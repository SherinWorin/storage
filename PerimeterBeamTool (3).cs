// =============================================================================
// AiTekla.TeklaIntegration — PerimeterBeamTool
// Phase 4 (Create objects) — Base Skid perimeter beam creation
// =============================================================================
//
// WHAT THIS DOES
//   - Click "Pick Perimeter Points" -> pick a polyline in Tekla (PICK_POLYGON).
//   - Choose a profile from the dropdown (the 4 confirmed base-skid profiles).
//   - Click "Create Beams" -> one Beam is created per consecutive point pair,
//     bottom-flush at a Z you specify, nudged inward from the picked line by
//     an offset you specify.
//   - Corner fitting uses Tekla's real "Fit beams and columns" catalog
//     component (Name "FitBeamsAndColumns", Number 42 on your install —
//     confirmed via Inquire Object, may differ on other installs), loading
//     the "swc_beam fit" attribute file you saved in Tekla with cutting and
//     welding turned off. Toggle with the checkbox in the UI.
//   - Door-beam splice logic (cut perimeter + insert door beam + P15 plates)
//     is intentionally NOT implemented yet — see the stub method at the
//     bottom, per your "slowly, next step" instruction.
//
// PROJECT SETUP (per AI_TEKLA_2025_MVP_PLAN.md Section 4)
//   - Visual Studio 2022, Class Library or WinForms App, .NET Framework 4.8.
//   - NuGet packages: Tekla.Structures, Tekla.Structures.Model (2025.0.0,
//     NOT a -betaXXX prerelease — check your feed).
//   - Gacless setup: add the TSAppConfigPatcherTask NuGet package and place
//     the project under Tekla's provided Directory.Build.Props, or Tekla
//     will fail to connect at Debug even though the code compiles fine.
//   - Run this as a standalone app while Tekla Structures 2025 is open with
//     a model loaded (matches Tekla's own documented pattern in "Code
//     example: Create beam using user input").
//
// API SURFACE USED — verified live against developer.tekla.com/doc/tekla-structures/2025/*
// on 2026-09-18, not from memory. Specifically checked:
//   - Beam class, Beam(Point,Point) constructor, Profile.ProfileString,
//     Material.MaterialString, Position, Class, Insert()
//   - Position.Depth / DepthOffset / Plane / PlaneOffset / Rotation / RotationOffset
//   - Position.DepthEnum {MIDDLE=0, FRONT=1, BEHIND=2}
//   - Position.PlaneEnum {MIDDLE=0, LEFT=1, RIGHT=2}
//   - Position.RotationEnum {FRONT=0, TOP=1, BACK=2, BELOW=3}
//   - Tekla.Structures.Model.UI.Picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON)
//   - Model.GetConnectionStatus(), Model.CommitChanges()
//   - Connection class: Name, Number, LoadAttributesFromFile(string),
//     UpVector, PositionType (PositionTypeEnum), AutoDirectionType
//     (AutoDirectionTypeEnum), SetPrimaryObject(Part), SetSecondaryObject(Part),
//     Insert() — verified against developer.tekla.com's Connection Class
//     page, using its exact confirmed code example, not guessed.
//
// TWO THINGS THAT NEED A LIVE TEKLA TEST (docs don't say which physical
// direction these resolve to — it depends on your model's work plane and
// which way you click the polyline). Both are exposed as dropdowns below
// so you can flip them without recompiling:
//   - Depth FRONT vs BEHIND  -> which one puts the profile ABOVE your
//     picked line (needed for "bottom sits at the picked Z").
//   - Plane LEFT vs RIGHT    -> which one is "inward" for your pick order.
//
// NOT YET DONE (left as a stub, on purpose):
//   - Door beam splice (cut perimeter, insert RHS60x30x4 + 6mm, add P15
//     plates, leave OPEN END corners uncapped). See PlaceDoorBeam_TODO().
//   - Reading profile/material from a project catalog instead of the
//     hard-coded list below.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures;             // PositionTypeEnum, AutoDirectionTypeEnum (for the Connection class)
using TSG = Tekla.Structures.Geometry3d;
using SDColor = System.Drawing.Color;   // disambiguates from Tekla.Structures.Model.UI.Color

namespace AiTekla.TeklaIntegration
{
    /// <summary>
    /// One confirmed base-skid profile entry for the dropdown.
    /// Source: base-skid-rules.md, Section 1.
    /// </summary>
    public class ProfileOption
    {
        public string Label;          // shown in the dropdown
        public string ProfileString;  // Tekla profile string
        public string Material = "S275";

        public ProfileOption(string label, string profileString)
        {
            Label = label;
            ProfileString = profileString;
        }

        public override string ToString() => Label;
    }

    public class PerimeterBeamForm : Form
    {
        // ---- UI controls ----
        private ComboBox cboProfile;
        private TextBox txtBaseZ;
        private TextBox txtInsetOffset;
        private ComboBox cboDepthDirection;   // which enum value = "profile sits above the line"
        private ComboBox cboInsetSide;        // which enum value = "inward" for this pick order
        private CheckBox chkFitCorners;
        private Button btnPickPoints;
        private Button btnCreate;
        private Button btnReset;
        private ListBox lstSegments;
        private Label lblStatus;
        private Label lblConnection;

        // ---- state ----
        private List<TSG.Point> pickedPoints = new List<TSG.Point>();

        public PerimeterBeamForm()
        {
            BuildUi();
            CheckConnection();
        }

        // -------------------------------------------------------------------
        // UI construction
        // -------------------------------------------------------------------
        private void BuildUi()
        {
            Text = "Base Skid — Perimeter Beam Tool (Phase 4, WIP)";
            Width = 480;
            Height = 560;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;

            var profiles = new List<ProfileOption>
            {
                new ProfileOption("RHS120*60*4.0  (Perimeter - green)", "RHS120*60*4.0"),
                new ProfileOption("RHS60*30*4.0   (Door beam - blue)",  "RHS60*30*4.0"),
                new ProfileOption("RHS40*20*3.0   (Partition/vanity/WC - yellow)", "RHS40*20*3.0"),
                new ProfileOption("SHS60*60*4.0   (WC cistern support - violet, conditional)", "SHS60*60*4.0"),
            };

            int y = 10;

            lblConnection = new Label { Left = 10, Top = y, Width = 450, Text = "Checking Tekla connection..." };
            Controls.Add(lblConnection);
            y += 28;

            Controls.Add(new Label { Left = 10, Top = y, Width = 150, Text = "Profile:" });
            cboProfile = new ComboBox { Left = 160, Top = y - 2, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cboProfile.Items.AddRange(profiles.Cast<object>().ToArray());
            cboProfile.SelectedIndex = 0; // default: main perimeter, as per your rule set
            Controls.Add(cboProfile);
            y += 30;

            Controls.Add(new Label { Left = 10, Top = y, Width = 150, Text = "Bottom elevation (Z, mm):" });
            txtBaseZ = new TextBox { Left = 160, Top = y - 2, Width = 100, Text = "0" };
            Controls.Add(txtBaseZ);
            var lblZHint = new Label
            {
                Left = 10, Top = y + 20, Width = 450, Height = 32,
                ForeColor = SDColor.DimGray,
                Text = "All picked points are snapped to this Z. Use 0 for now; " +
                       "use +3 once you're ready to apply the confirmed GRP offset rule."
            };
            Controls.Add(lblZHint);
            y += 56;

            Controls.Add(new Label { Left = 10, Top = y, Width = 150, Text = "Inward offset (mm):" });
            txtInsetOffset = new TextBox { Left = 160, Top = y - 2, Width = 100, Text = "0" };
            Controls.Add(txtInsetOffset);
            y += 30;

            Controls.Add(new Label { Left = 10, Top = y, Width = 150, Text = "Inward = " });
            cboInsetSide = new ComboBox { Left = 160, Top = y - 2, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cboInsetSide.Items.AddRange(new object[] { "LEFT", "RIGHT" });
            cboInsetSide.SelectedIndex = 0;
            Controls.Add(cboInsetSide);
            var lblInsetHint = new Label
            {
                Left = 10, Top = y + 20, Width = 450, Height = 30,
                ForeColor = SDColor.DimGray,
                Text = "Untested direction — create one test beam, check which side it lands on, flip if needed."
            };
            Controls.Add(lblInsetHint);
            y += 56;

            Controls.Add(new Label { Left = 10, Top = y, Width = 150, Text = "Bottom-flush = " });
            cboDepthDirection = new ComboBox { Left = 160, Top = y - 2, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cboDepthDirection.Items.AddRange(new object[] { "FRONT", "BEHIND" });
            cboDepthDirection.SelectedIndex = 0;
            Controls.Add(cboDepthDirection);
            var lblDepthHint = new Label
            {
                Left = 10, Top = y + 20, Width = 450, Height = 30,
                ForeColor = SDColor.DimGray,
                Text = "Untested direction — whichever value puts the profile ABOVE your picked line."
            };
            Controls.Add(lblDepthHint);
            y += 60;

            chkFitCorners = new CheckBox
            {
                Left = 10, Top = y, Width = 440,
                Text = "Fit corners (Tekla \"Fit beams and columns\", #42, \"swc_beam fit\" settings)",
                Checked = true
            };
            Controls.Add(chkFitCorners);
            y += 28;

            btnPickPoints = new Button { Left = 10, Top = y, Width = 200, Height = 32, Text = "Pick Perimeter Points" };
            btnPickPoints.Click += BtnPickPoints_Click;
            Controls.Add(btnPickPoints);

            btnReset = new Button { Left = 220, Top = y, Width = 100, Height = 32, Text = "Reset" };
            btnReset.Click += (s, e) => { pickedPoints.Clear(); RefreshSegmentList(); };
            Controls.Add(btnReset);
            y += 42;

            lstSegments = new ListBox { Left = 10, Top = y, Width = 440, Height = 140 };
            Controls.Add(lstSegments);
            y += 150;

            btnCreate = new Button { Left = 10, Top = y, Width = 200, Height = 36, Text = "Create Beams", Enabled = false };
            btnCreate.Click += BtnCreate_Click;
            Controls.Add(btnCreate);
            y += 44;

            lblStatus = new Label { Left = 10, Top = y, Width = 450, Height = 40, ForeColor = SDColor.DarkBlue };
            Controls.Add(lblStatus);
        }

        // -------------------------------------------------------------------
        // Connection check (Phase 1 behaviour — read-only, no model changes)
        // -------------------------------------------------------------------
        private void CheckConnection()
        {
            try
            {
                var model = new Model();
                bool connected = model.GetConnectionStatus();
                lblConnection.Text = connected
                    ? "Connected to Tekla Structures."
                    : "NOT connected — open Tekla Structures with a model, then restart this tool.";
                lblConnection.ForeColor = connected ? SDColor.DarkGreen : SDColor.DarkRed;
                btnPickPoints.Enabled = connected;
            }
            catch (Exception ex)
            {
                lblConnection.Text = "Connection check failed: " + ex.Message;
                lblConnection.ForeColor = SDColor.DarkRed;
                btnPickPoints.Enabled = false;
            }
        }

        // -------------------------------------------------------------------
        // Point picking
        // -------------------------------------------------------------------
        private void BtnPickPoints_Click(object sender, EventArgs e)
        {
            try
            {
                double baseZ = ParseDoubleOrDefault(txtBaseZ.Text, 0.0);

                var picker = new Picker();
                // PICK_POLYGON: user clicks as many points as needed, ends the
                // sequence with the standard Tekla "interrupt" (middle-click / Esc).
                ArrayList raw = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON);

                pickedPoints.Clear();
                foreach (TSG.Point p in raw)
                {
                    // Snap every picked point to the requested bottom elevation,
                    // so an imprecise click in a plan view can't create a sloped beam.
                    pickedPoints.Add(new TSG.Point(p.X, p.Y, baseZ));
                }

                RefreshSegmentList();
                lblStatus.Text = pickedPoints.Count >= 2
                    ? $"Picked {pickedPoints.Count} points -> {pickedPoints.Count - 1} beam segment(s) ready."
                    : "Pick at least 2 points to form a segment.";
                btnCreate.Enabled = pickedPoints.Count >= 2;
            }
            catch (Exception ex)
            {
                // Covers the picker being interrupted (Esc / no points picked),
                // which Tekla's own docs say throws rather than returning empty.
                lblStatus.Text = "Point picking cancelled or failed: " + ex.Message;
                btnCreate.Enabled = pickedPoints.Count >= 2;
            }
        }

        private void RefreshSegmentList()
        {
            lstSegments.Items.Clear();
            for (int i = 0; i < pickedPoints.Count - 1; i++)
            {
                var a = pickedPoints[i];
                var b = pickedPoints[i + 1];
                double len = System.Math.Sqrt(
                    System.Math.Pow(b.X - a.X, 2) +
                    System.Math.Pow(b.Y - a.Y, 2) +
                    System.Math.Pow(b.Z - a.Z, 2));
                lstSegments.Items.Add($"Segment {i + 1}: ({a.X:F0},{a.Y:F0},{a.Z:F0}) -> " +
                                       $"({b.X:F0},{b.Y:F0},{b.Z:F0})   length = {len:F0} mm");
            }
        }

        // -------------------------------------------------------------------
        // Beam creation — the actual Phase 4 write to the model
        // -------------------------------------------------------------------
        private void BtnCreate_Click(object sender, EventArgs e)
        {
            if (pickedPoints.Count < 2)
            {
                MessageBox.Show("Pick at least 2 points first.");
                return;
            }

            var profile = (ProfileOption)cboProfile.SelectedItem;
            double inset = ParseDoubleOrDefault(txtInsetOffset.Text, 0.0);

            Position.DepthEnum depthDir = cboDepthDirection.SelectedItem.ToString() == "FRONT"
                ? Position.DepthEnum.FRONT
                : Position.DepthEnum.BEHIND;

            Position.PlaneEnum planeDir = cboInsetSide.SelectedItem.ToString() == "LEFT"
                ? Position.PlaneEnum.LEFT
                : Position.PlaneEnum.RIGHT;

            // Safety confirmation before writing to the model (Section 9 of the
            // master plan: object creation is a MEDIUM-risk operation).
            var confirm = MessageBox.Show(
                $"Create {pickedPoints.Count - 1} beam(s)?\n\n" +
                $"Profile: {profile.ProfileString}\nMaterial: {profile.Material}\n" +
                $"Bottom Z: {txtBaseZ.Text}\nInward offset: {inset} mm ({planeDir})\n" +
                $"Depth reference: {depthDir}\n\n" +
                "This will write to the currently open Tekla model.",
                "Confirm beam creation", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

            if (confirm != DialogResult.OK) return;

            int created = 0, failed = 0;
            var errors = new List<string>();
            var model = new Model();

            // Track the actual Beam objects (indexed same as pickedPoints[i]->[i+1])
            // so we can fit adjacent beams together at shared corner points afterward.
            var beams = new Beam[pickedPoints.Count - 1];

            for (int i = 0; i < pickedPoints.Count - 1; i++)
            {
                try
                {
                    var beam = new Beam(pickedPoints[i], pickedPoints[i + 1]);
                    beam.Profile.ProfileString = profile.ProfileString;
                    beam.Material.MaterialString = profile.Material;

                    // "0 level bottom, orientation as taught": leave Rotation at
                    // its default (FRONT/0 offset) — for a beam lying flat in a
                    // horizontal work plane this keeps the profile's height
                    // dimension vertical (matches the 120/30/20mm-vertical rule,
                    // confirmed earlier from the IFC bounding-box check).
                    beam.Position.Depth = depthDir;
                    beam.Position.DepthOffset = 0;

                    beam.Position.Plane = planeDir;
                    beam.Position.PlaneOffset = inset;

                    bool ok = beam.Insert();
                    if (ok) { created++; beams[i] = beam; }
                    else { failed++; errors.Add($"Segment {i + 1}: Insert() returned false."); }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"Segment {i + 1}: {ex.Message}");
                }
            }

            int cornersFitted = 0, cornersFailed = 0;
            if (chkFitCorners.Checked)
            {
                FitAllCorners(beams, pickedPoints, errors, ref cornersFitted, ref cornersFailed);
            }

            bool committed = false;
            try { committed = model.CommitChanges("AiTekla: create perimeter beams"); }
            catch (Exception ex) { errors.Add("CommitChanges failed: " + ex.Message); }

            lblStatus.Text = $"Created {created}, failed {failed}. " +
                              $"Corners fitted {cornersFitted}, failed {cornersFailed}. " +
                              $"Commit: {(committed ? "OK" : "FAILED")}." +
                              (errors.Count > 0 ? "\n" + string.Join("\n", errors) : "");
        }

        // -------------------------------------------------------------------
        // Corner joining — uses Tekla's real "Fit beams and columns" catalog
        // component (Name "FitBeamsAndColumns", Number 42 — confirmed on
        // YOUR install via Inquire Object on a manually-placed instance;
        // this Number can differ between Tekla installs/environments, so if
        // this tool is ever used on a different machine, re-check it there).
        //
        // Verified Connection class usage pattern against developer.tekla.com
        // (Connection Class code example, Tekla.Structures.Model), not
        // guessed: Name, Number, LoadAttributesFromFile(string),
        // SetPrimaryObject(Part), SetSecondaryObject(Part), Insert().
        //
        // Settings (no cut, no weld) come from the "swc_beam fit" attribute
        // file you saved in Tekla's own dialog — the code does not set
        // individual fitting/gap/weld properties itself, since the internal
        // attribute-name strings for this component aren't published
        // anywhere I could verify. If you want a DIFFERENT behavior at some
        // corners later (e.g. an actual mitre with weld for internal
        // joints), save another named attribute file in Tekla and pass its
        // name into PlaceFitBeamsAndColumns below.
        // -------------------------------------------------------------------
        private const string FitBeamsAndColumnsAttributeFile = "swc_beam fit";

        private void FitAllCorners(Beam[] beams, List<TSG.Point> points, List<string> errors,
                                    ref int fitted, ref int failed)
        {
            int n = beams.Length;
            if (n < 2) return; // need at least 2 segments to have an interior corner

            for (int i = 1; i < n; i++)
            {
                TryFitCorner(beams[i - 1], beams[i], errors, ref fitted, ref failed, $"corner at segment {i}/{i + 1}");
            }

            // Closed loop: if the very first and very last picked points are
            // (nearly) the same point, fit the last beam back to the first.
            var first = points[0];
            var last = points[points.Count - 1];
            double closeDist = System.Math.Sqrt(
                System.Math.Pow(last.X - first.X, 2) + System.Math.Pow(last.Y - first.Y, 2));
            if (closeDist < 5.0 && beams[0] != null && beams[n - 1] != null)
            {
                TryFitCorner(beams[n - 1], beams[0], errors, ref fitted, ref failed, "closing corner (last -> first)");
            }
        }

        private void TryFitCorner(Beam primary, Beam secondary, List<string> errors,
                                   ref int fitted, ref int failed, string label)
        {
            if (primary == null || secondary == null)
            {
                failed++;
                errors.Add($"{label}: skipped, one of the adjoining beams failed to create.");
                return;
            }

            try
            {
                var connection = new Connection
                {
                    Name = "FitBeamsAndColumns",
                    Number = 42
                };
                connection.LoadAttributesFromFile(FitBeamsAndColumnsAttributeFile);
                connection.UpVector = new TSG.Vector(0, 0, 1000);
                connection.PositionType = PositionTypeEnum.COLLISION_PLANE;
                connection.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;
                connection.SetPrimaryObject(primary);
                connection.SetSecondaryObject(secondary);

                bool ok = connection.Insert();
                if (ok) fitted++;
                else { failed++; errors.Add($"{label}: Connection Insert() returned false."); }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{label}: {ex.Message}");
            }
        }

        // -------------------------------------------------------------------
        // NOT IMPLEMENTED YET — placeholder for the next step you asked to
        // defer: cutting the perimeter at a door, inserting the door beam
        // (length = opening width + 6mm, centered), and adding P15 end
        // plates on both cut faces (never at OPEN END lifting corners).
        // -------------------------------------------------------------------
        private void PlaceDoorBeam_TODO(TSG.Point doorCenter, double doorWidth, TSG.Point beamDirection)
        {
            // TODO (next iteration):
            //  1. Find which perimeter beam this door falls on.
            //  2. Split it at (doorCenter - (doorWidth/2+3)) and (doorCenter + (doorWidth/2+3))
            //     — i.e. leave the 4mm gap on each side, per base-skid-rules.md Section 4.
            //  3. Shorten the two remaining perimeter stubs to their cut points.
            //  4. Insert a new RHS60*30*4.0 beam of length (doorWidth + 6), centered.
            //  5. Insert two PL4*60*120 (P15) end plates, one per cut face, standing
            //     vertical, spanning the full 120mm height (per confirmed IFC geometry).
            throw new NotImplementedException("Door-beam splice logic is deferred — see base-skid-rules.md Section 4.");
        }

        private static double ParseDoubleOrDefault(string text, double fallback)
        {
            double v;
            return double.TryParse(text, out v) ? v : fallback;
        }

        // -------------------------------------------------------------------
        // Entry point — run this as a standalone app while Tekla is open.
        // -------------------------------------------------------------------
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new PerimeterBeamForm());
        }
    }
}
