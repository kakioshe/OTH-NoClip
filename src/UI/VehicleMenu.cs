using System;
using System.Globalization;
using UnityEngine;

// Presentation only: the runtime executes repair, tuning and protection commands.
internal sealed class VehicleMenu
{
    private readonly string[] drafts = new string[5];
    private readonly bool[] editing = new bool[5];
    private string inputStatus = "", actionStatus = "";
    private readonly VehicleRepair repair;
    private readonly VehicleFlip flip;
    private readonly VehicleControls controls;
    private readonly VehicleAssists assists;
    private readonly VehicleOverdrive overdrive;
    private readonly VehicleTuning tuning;
    private readonly Action preferencesChanged;
    private GUIStyle label, detail, button, field, section, badge;
    private int pinned = -1;
    private static readonly VehicleSetting[] SimpleSettings = { VehicleSetting.SpeedLimit, VehicleSetting.Power, VehicleSetting.Grip, VehicleSetting.Steering, VehicleSetting.Brakes };
    private static readonly string[] SimpleTitles = { "Top speed", "Acceleration", "Grip", "Steering", "Braking" };
    private static readonly string[] SimpleHelp = {
        "Choose how much faster you want the car to go, from 1x to 3x. Calculates the speed limit, forward gearing and supporting power together. Stop to apply a top-speed change. Actual speed depends on the road, traction and car. 1x is stock.",
        "Choose stronger or gentler acceleration, from 0.5x to 3x. Automatically accounts for the selected top speed so taller gearing does not simply weaken acceleration. Wheelspin and terrain still affect the result. 1x is stock.",
        "Adjust tire grip from 0.5x to 2x. More grip helps acceleration, braking and turning. 1x is stock.",
        "Adjust steering angle from 0.5x to 1.5x. Higher values give tighter turns but can feel unstable at speed. 1x is stock.",
        "Adjust braking strength from 0.5x to 2x. Stopping distance also depends on tire grip. 1x is stock."
    };
    private static readonly string[] Titles = { "Repair car", "Flip car", "God mode", "Vehicle modifiers", "Power", "Speed limit", "Grip", "Steering angle", "Braking strength", "Water immunity", "Spider mode", "Tuning presets", "High-gear speed boost" };
    private static readonly string[] Help = {
        "Repairs your current single-player car through the game's healing operation without using an item. Does not enable God mode or change performance modifiers.",
        "Turns your car upright, retains its horizontal heading, lifts it 0.75 metres and stops motion. Disable flight first. Use in open space; nearby terrain can obstruct recovery.",
        "Prevents new damage through the local car's damage multiplier and damage handler. Existing damage remains until repaired. Does not promise flooding or other environmental immunity.",
        "Applies the five sliders to your current car's legitimate settings. Off restores owned baselines and keeps slider preferences. God mode and repairs are independent. Performance modifiers pause during flight. Both switches reset on car/session loss.",
        "Scales engine torque and existing torque/power cap values relative to the car's current baseline. More power does not guarantee proportional acceleration or speed.",
        "Scales the car's existing engine speed-limit setting. 1x retains stock. Does not force-enable a disabled limiter or impose a hard physical speed cap; gearing and available power still matter.",
        "Scales each wheel's observed grip multiplier. More grip affects acceleration, braking and turning together. Physical handling and teardown restoration need combined runtime testing.",
        "Scales the maximum steering angle. This changes wheel angle rather than input sensitivity; high values can make fast driving unstable.",
        "Scales service-brake torque. Handbrake and brake balance are unchanged. Stopping distance also depends on grip.",
        "Experimental: prevents new engine flooding and suppresses wheel terrain resistance while your car touches water. Tire grip, brakes, water detection and drying remain active. Does not dry an already flooded engine or provide buoyancy. Starts off and resets on car/session loss.",
        "Experimental surface driving: adds its own motor, adhesion and surface alignment for slopes, walls and ceilings. Use normal driving and steering controls. Targets 36 km/h forward and 18 km/h reverse independently of tuning sliders; release throttle or use the handbrake to hold. Requires a nearby surface; releases in air and pauses during flight. Start slowly on a slope before trying walls.",
        "Replaces and saves all five tuning values. Stock: all 1x. Trail: 2/1.5/1.5/1.1/1.5. Climb: 3/1/2/1/2. Fast: 5/5/1.5/0.75/2 (power/speed/grip/steering/brakes). Existing activation stays unchanged, so active modifiers update immediately. Presets never turn on God mode, water immunity, Spider mode or overdrive. Power and speed can also be entered up to 10x.",
        "Makes the forward high gears run faster at the same engine RPM. 1x is stock; 2x gives twice the gear speed per engine revolution but less pulling force. In Detailed, adjust Power and Speed limit separately, or choose Simple to calculate them automatically. Stop to apply a change. Only applies with tuning ON. Low and reverse ratios stay stock. This setting is session-only."
    };

    internal VehicleMenu(VehicleRepair repair, VehicleFlip flip, VehicleControls controls, VehicleAssists assists, VehicleOverdrive overdrive, VehicleTuning tuning, Action preferencesChanged)
    {
        this.repair = repair;
        this.flip = flip;
        this.controls = controls;
        this.assists = assists;
        this.overdrive = overdrive;
        this.tuning = tuning;
        this.preferencesChanged = preferencesChanged;
    }

    internal void Draw(Rect area)
    {
        InitializeStyles();
        var current = Event.current;
        string focused = GUI.GetNameOfFocusedControl();
        if (current.type == EventType.MouseDown)
            for (int index = 0; index < drafts.Length; index++)
                if (focused == "vehicle-value-" + index &&
                    !new Rect(area.xMax - 152, area.y + RowTop(index + 4), 74, 27).Contains(current.mousePosition))
                    GUI.FocusControl("");
        int hovered = -1;
        for (int row = 0; row < Titles.Length; row++)
        {
            if (tuning.Simple && row >= 11) continue;
            if (InfoRect(area, row).Contains(current.mousePosition)) hovered = row;
        }
        if (pinned >= 0)
        {
            Rect popup = PopupRect(area, pinned);
            if (current.type == EventType.MouseDown && !InfoRect(area, pinned).Contains(current.mousePosition) && !popup.Contains(current.mousePosition)) pinned = -1;
            if (pinned >= 0 && popup.Contains(current.mousePosition) &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.ScrollWheel)) current.Use();
        }
        Fill(new Rect(area.x - 6, area.y, area.width + 12, 204), MenuTheme.Card);
        GUI.Label(new Rect(area.x + 6, area.y + 8, 230, 25), "VEHICLE ACTIONS", section);
        Fill(new Rect(area.x - 6, area.y + 216, area.width + 12, 296), MenuTheme.Card);
        for (int row = 0; row < Titles.Length; row++)
        {
            if (tuning.Simple && row >= 11) continue;
            float top = area.y + RowTop(row);
            GUI.Label(new Rect(area.x + 6, top, 250, 27), Title(row), row == 3 ? section : label);
            var action = new Rect(area.xMax - 174, top, 120, 27);
            if (row <= 1)
            {
                bool pending = row == 0 ? repair.Pending : flip.Pending;
                bool previous = GUI.enabled;
                GUI.enabled = previous && !pending;
                if (Button(action, pending ? "Queued" : row == 0 ? "Repair" : "Flip"))
                {
                    if (row == 0) { repair.Request(); actionStatus = "repair"; }
                    else { flip.Request(); actionStatus = "flip"; }
                }
                GUI.enabled = previous;
            }
            else if (row == 2)
            {
                if (Button(action, controls.GodMode ? "On" : "Off", controls.GodMode))
                { controls.GodMode = !controls.GodMode; actionStatus = "controls"; }
            }
            else if (row == 3)
            {
                DrawMode(area, top);
                Fill(action, tuning.State == "ON" ? new Color(0.10f, 0.40f, 0.30f) : tuning.State == "OFF" ? new Color(0.25f, 0.28f, 0.31f) : new Color(0.40f, 0.30f, 0.10f));
                GUI.Label(action, "TUNING " + tuning.State, badge);
            }
            else if (row == 9 || row == 10)
            {
                bool enabled = row == 9 ? assists.Flooding : assists.Climb;
                if (Button(action, enabled ? "On" : "Off", enabled))
                {
                    if (row == 9) assists.Flooding = !enabled; else assists.Climb = !enabled;
                    actionStatus = "assists";
                }
            }
            else if (row == 11) DrawPresets(area, top);
            else if (row == 12) DrawOverdrive(area, top);
            else DrawSetting(area, top, row);
            if (Button(InfoRect(area, row), "i", pinned == row)) pinned = pinned == row ? -1 : row;
        }
        GUI.Label(new Rect(area.x + 6, area.y + 256, area.width - 224, 27), tuning.ActivationText, detail);
        if (Button(new Rect(area.xMax - 208, area.y + 256, 174, 27), controls.Modifiers ? "Disable tuning" : "Enable tuning", controls.Modifiers))
        { controls.Modifiers = !controls.Modifiers; actionStatus = "controls"; }
        if (Button(new Rect(area.x, area.y + 546, 166, 30), "Restore stock"))
        {
            tuning.RestoreStock();
            Array.Clear(drafts, 0, drafts.Length);
            GUI.FocusControl(""); Array.Clear(editing, 0, editing.Length); inputStatus = "";
            actionStatus = "controls";
            preferencesChanged();
        }
        string status = inputStatus.Length > 0 ? inputStatus : tuning.Status.Length > 0 ? tuning.Status : overdrive.Status.Length > 0 ? overdrive.Status : actionStatus == "assists" ? assists.Status : actionStatus == "flip" ? flip.Status :
            actionStatus == "repair" ? repair.Status : controls.Status;
        GUI.Label(new Rect(area.x, area.y + 516, area.width, 28), status, detail);
        int help = hovered >= 0 ? hovered : pinned;
        if (help < 0) return;
        Rect box = PopupRect(area, help);
        Fill(new Rect(box.x + 3, box.y + 4, box.width, box.height), new Color(0, 0, 0, 0.3f));
        Fill(box, new Color(0.24f, 0.34f, 0.40f));
        Fill(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), new Color(0.10f, 0.14f, 0.18f));
        GUI.Label(new Rect(box.x + 14, box.y + 14, box.width - 28, box.height - 28), HelpText(help), detail);
    }

    private float RowTop(int row) => tuning.Simple && row >= 4 && row <= 8 ? 288 + (row - 4) * 36 : row < 3 ? 41 + row * 32 : row == 3 ? 224 :
        row == 9 ? 137 : row == 10 ? 169 : row == 11 ? 288 : row == 12 ? 480 : 320 + (row - 4) * 32;
    private Rect InfoRect(Rect area, int row) => new Rect(area.xMax - 34, area.y + RowTop(row), 30, 27);

    private string Title(int row) => row == 3 ? "VEHICLE TUNING" : tuning.Simple && row >= 4 && row <= 8 ? SimpleTitles[row - 4] : Titles[row];
    private string HelpText(int row) => row == 3 ?
        "Simple calculates power, speed limit and gearing for you. Detailed exposes individual controls and presets. Each mode keeps its own values. Switching modes turns tuning off. Click Enable tuning to apply your settings; the status reads ON only after performance values are applied. Tuning starts OFF each session. God mode and assists are independent." :
        tuning.Simple && row >= 4 && row <= 8 ? SimpleHelp[row - 4] : Help[row];

    private void DrawMode(Rect area, float top)
    {
        bool next = tuning.Simple;
        if (Button(new Rect(area.x + 256, top, 104, 27), "Simple", tuning.Simple)) next = true;
        if (Button(new Rect(area.x + 366, top, 104, 27), "Detailed", !tuning.Simple)) next = false;
        if (next == tuning.Simple) return;
        tuning.SetMode(next);
        pinned = -1;
        GUI.FocusControl("");
        Array.Clear(drafts, 0, drafts.Length); Array.Clear(editing, 0, editing.Length);
        inputStatus = "";
        actionStatus = "controls";
    }

    private void DrawOverdrive(Rect area, float top)
    {
        float value = tuning.DetailedGearing;
        float next = GUI.HorizontalSlider(new Rect(area.x + 256, top + 9, area.width - 422, 20), value, 1, 3);
        next = (float)Math.Round(next * 20) / 20;
        if (next != value) { tuning.SetGearing(next); actionStatus = "controls"; }
        GUI.Label(new Rect(area.xMax - 152, top, 94, 27), tuning.DetailedGearing.ToString("0.##", CultureInfo.InvariantCulture) + "x", detail);
    }

    private void DrawPresets(Rect area, float top)
    {
        foreach (VehiclePreset preset in Enum.GetValues(typeof(VehiclePreset)))
        {
            if (!Button(new Rect(area.x + 256 + (int)preset * 110, top, 104, 27), preset.ToString())) continue;
            VehiclePresets.Apply(controls.Preferences, preset);
            Array.Clear(drafts, 0, drafts.Length);
            Array.Clear(editing, 0, editing.Length);
            GUI.FocusControl("");
            inputStatus = "";
            preferencesChanged();
        }
    }

    private void DrawSetting(Rect area, float top, int row)
    {
        int index = row - 4;
        VehicleSetting setting = tuning.Simple ? SimpleSettings[index] : (VehicleSetting)index;
        float value = tuning.Preferences.Get(setting);
        float next = GUI.HorizontalSlider(new Rect(area.x + 256, top + 9, area.width - 422, 20), value, tuning.Minimum(setting), tuning.Maximum(setting));
        string name = "vehicle-value-" + index;
        string focus = GUI.GetNameOfFocusedControl();
        if (drafts[index] == null || focus != name && !editing[index]) drafts[index] = value.ToString("R", CultureInfo.InvariantCulture);
        bool enter = focus == name && Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        GUI.SetNextControlName(name);
        drafts[index] = GUI.TextField(new Rect(area.xMax - 152, top, 74, 27), drafts[index], 16, field);
        GUI.Label(new Rect(area.xMax - 73, top, 20, 27), "x", detail);
        string newFocus = GUI.GetNameOfFocusedControl();

        bool left = editing[index] && newFocus != name;
        if (enter || left)
        {
            if (VehicleValueInput.TryApply(tuning.Preferences, setting, drafts[index], tuning.Minimum(setting), tuning.Maximum(setting)))
            { preferencesChanged(); inputStatus = ""; }
            else inputStatus = "Enter a number from " + tuning.Minimum(setting).ToString(CultureInfo.InvariantCulture) + " to " + tuning.Maximum(setting).ToString(CultureInfo.InvariantCulture) + " for " + Title(row) + ".";
            drafts[index] = tuning.Preferences.Get(setting).ToString("R", CultureInfo.InvariantCulture);
            if (enter) { GUI.FocusControl(""); Event.current.Use(); }
            editing[index] = false;
        }
        else if (newFocus == name) editing[index] = true;
        if (next != value)
        {
            next = (float)Math.Round(next * 20) / 20;
            if (next != value && tuning.Set(setting, next))
            {
                preferencesChanged(); drafts[index] = next.ToString("R", CultureInfo.InvariantCulture);
                inputStatus = "";
            }
        }
    }
    private Rect PopupRect(Rect area, int row)
    {
        Rect anchor = InfoRect(area, row);
        float height = detail.CalcHeight(new GUIContent(HelpText(row)), 282) + 28;
        float top = anchor.yMax + 8;
        if (top + height > area.yMax) top = anchor.y - height - 8;
        return new Rect(anchor.xMax - 310, top, 310, height);
    }

    private void InitializeStyles()
    {
        if (label != null) return;
        label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        section = MenuTheme.Section(label);
        detail = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        detail.normal.textColor = MenuTheme.Detail;
        field = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        badge = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        badge.normal.textColor = Color.white;
        button = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        foreach (var state in new[] { button.normal, button.hover, button.active, button.focused })
        {
            state.background = Texture2D.whiteTexture;
            state.textColor = Color.white;
        }
        button.hover.textColor = MenuTheme.Accent;
        button.active.textColor = MenuTheme.Accent;
    }
    private bool Button(Rect rect, string text, bool selected = false)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.16f, 0.42f, 0.39f) : new Color(0.17f, 0.22f, 0.28f);
        try { return GUI.Button(rect, text, button); }
        finally { GUI.backgroundColor = previous; }
    }
    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        try { GUI.DrawTexture(rect, Texture2D.whiteTexture); }
        finally { GUI.color = previous; }
    }
}
