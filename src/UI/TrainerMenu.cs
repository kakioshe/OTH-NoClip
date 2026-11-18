using System;
using UnityEngine;
internal sealed class TrainerMenu
{
    private readonly TrainerInput input;
    private readonly BindingStore settings;
    private readonly TrainerLogger logger;
    private readonly Action<Rect> drawVehicle;
    private bool vehicleSelected;
    internal TrainerBindings Bindings { get; private set; } = new();
    internal bool Visible { get; private set; }

    internal bool BlocksActions => Visible || waitForRelease || !input.Focused;
    private bool waitForRelease = true, captureReady, capturePad;
    private TrainerAction? capture;
    private string message = "";
    private CursorLockMode previousLock;
    private bool previousCursor;
    private GUIStyle titleStyle, labelStyle, buttonStyle, noteStyle;
    internal TrainerMenu(TrainerInput input, BindingStore settings, TrainerLogger logger, Action<Rect> drawVehicle = null)
    {
        this.input = input; this.settings = settings; this.logger = logger;
        this.drawVehicle = drawVehicle;

        try
        {
            Bindings = settings.Load(out int rejected);
            if (rejected > 0) message = $"Ignored {rejected} invalid or conflicting settings. Review bindings below.";
        }
        catch (Exception e) { ReportFailure("Could not load settings; defaults are active", e); }
    }
    internal void Update()
    {
        if (!input.Focused) { Close(); waitForRelease = true; return; }

        if (waitForRelease)
        {
            if (input.Released) waitForRelease = false;

            return;
        }
        if (capture.HasValue)
        {
            if (input.Pressed(0x1B)) { capture = null; message = ""; waitForRelease = true; return; }

            if (!captureReady) { captureReady = input.Released; return; }

            string error;

            bool changed;

            if (capturePad)
            {
                PadControl control = input.CapturePad();

                if (control == PadControl.None) return;

                changed = Bindings.SetPad(capture.Value, control, out error);
            }
            else
            {
                int key = input.CaptureKey();

                if (key == 0) return;

                changed = Bindings.SetKey(capture.Value, key, input.CaptureModifiers(key), out error);
            }
            if (changed) { capture = null; Save(); }

            else { message = error; captureReady = false; }

            return;
        }
        if (input.Pressed(Bindings, TrainerAction.Menu))
        {
            if (Visible) Close(); else Open();

            waitForRelease = true;
        }
        else if (Visible && input.Pressed(0x1B)) { Close(); waitForRelease = true; }
    }
    private void Open()
    {
        previousLock = Cursor.lockState; previousCursor = Cursor.visible;

        Visible = true; UnlockCursor();
    }
    internal void MaintainCursor() { if (Visible && input.Focused) UnlockCursor(); }

    private static void UnlockCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

    internal void Close()
    {
        if (!Visible) return;

        Visible = false; capture = null;

        Cursor.lockState = previousLock; Cursor.visible = previousCursor;
    }
    private void Save()
    {
        try { settings.Save(Bindings); message = ""; }

        catch (Exception e) { ReportFailure("Settings work for this session, but could not be saved", e); }
    }
    private void ReportFailure(string text, Exception error)
    {
        message = text + ": " + error.Message;

        logger.LogWarning(message);
    }
    private void BeginCapture(TrainerAction action, bool controller)
    {
        capture = action; capturePad = controller; captureReady = false;

        message = controller ? "Use a controller input - Esc cancels" : "Press a shortcut - Esc cancels";
    }
    private static readonly Color Panel = MenuTheme.Panel;
    private static readonly Color Card = MenuTheme.Card;
    private static readonly Color Accent = MenuTheme.Accent;
    private GUIStyle sectionStyle;
    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;

        GUI.color = color;

        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = previous;
    }
    private void InitializeStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };

        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };

        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter };

        buttonStyle.normal.background = Texture2D.whiteTexture;

        buttonStyle.hover.background = Texture2D.whiteTexture;

        buttonStyle.active.background = Texture2D.whiteTexture;

        buttonStyle.normal.textColor = Color.white;

        buttonStyle.hover.textColor = Accent;

        buttonStyle.active.textColor = Accent;

        noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };

        noteStyle.normal.textColor = MenuTheme.Detail;

        sectionStyle = MenuTheme.Section(labelStyle);
    }
    private bool Button(Rect rect, string text, bool selected = false)
    {
        Color previous = GUI.backgroundColor;

        int previousSize = buttonStyle.fontSize;

        if (text.Length > 25) buttonStyle.fontSize = 12;

        GUI.backgroundColor = selected ? new Color(0.16f, 0.42f, 0.39f) : new Color(0.17f, 0.22f, 0.28f);

        bool clicked = GUI.Button(rect, text, buttonStyle);

        GUI.backgroundColor = previous;

        buttonStyle.fontSize = previousSize;

        return clicked;
    }
    private void DrawRow(float x, float top, TrainerAction action)
    {
        GUI.Label(new Rect(x + 30, top, 238, 28), TrainerBindings.Label(action), labelStyle);

        if (action == TrainerAction.Menu)
        {
            GUI.Label(new Rect(x + 270, top, 190, 27), "Insert", labelStyle);

            GUI.Label(new Rect(x + 520, top, 218, 27), "-", labelStyle);

            return;
        }
        bool listening = capture == action;

        if (Button(new Rect(x + 270, top, 190, 27), listening && !capturePad ? "Press shortcut..." : Bindings.ShortcutName(action), listening && !capturePad)) BeginCapture(action, false);

        if (Button(new Rect(x + 464, top, 32, 27), "×")) { capture = null; Bindings.SetKey(action, 0, out _); Save(); }

        if (Button(new Rect(x + 520, top, 218, 27), listening && capturePad ? "Use control..." : TrainerBindings.PadName(Bindings.Pad(action)), listening && capturePad)) BeginCapture(action, true);

        if (Button(new Rect(x + 742, top, 32, 27), "×")) { capture = null; Bindings.SetPad(action, PadControl.None, out _); Save(); }
    }
    internal void Draw()
    {
        if (!Visible) return;

        InitializeStyles();

        Matrix4x4 previousMatrix = GUI.matrix;

        float scale = Math.Min(1.3f, Math.Min(Screen.width / 832f, Screen.height / 756f));

        float x = (Screen.width / scale - 800f) / 2f, y = (Screen.height / scale - 724f) / 2f;

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

        try
        {
            Fill(new Rect(x, y, 800, 724), Panel);

            Fill(new Rect(x, y, 4, 724), Accent);

            GUI.Label(new Rect(x + 24, y + 16, 650, 40), "Port's Trainer", titleStyle);

            if (Button(new Rect(x + 690, y + 20, 84, 30), "Close")) { Close(); waitForRelease = true; }
            if (Button(new Rect(x + 24, y + 72, 148, 36), "Movement", !vehicleSelected)) SelectTab(false);
            if (drawVehicle != null && Button(new Rect(x + 180, y + 72, 148, 36), "Vehicle", vehicleSelected)) SelectTab(true);
            Fill(new Rect(x + 24, y + 113, 752, 1), Card);
            if (vehicleSelected && drawVehicle != null) drawVehicle(new Rect(x + 24, y + 130, 752, 534));
            else DrawMovement(x, y);
            GUI.Label(new Rect(x + 580, y + 683, 194, 24), "Insert / Esc to close", noteStyle);
        }
        finally { GUI.matrix = previousMatrix; }
    }

    private void SelectTab(bool vehicle)
    {
        if (vehicleSelected == vehicle) return;
        vehicleSelected = vehicle;
        capture = null;
        message = "";
    }

    private void DrawMovement(float x, float y)
    {
        Fill(new Rect(x + 18, y + 130, 764, 134), Card);
        GUI.Label(new Rect(x + 30, y + 138, 230, 25), "MOVEMENT ACTIONS", sectionStyle);
        GUI.Label(new Rect(x + 270, y + 138, 210, 24), "Keyboard", noteStyle);
        GUI.Label(new Rect(x + 520, y + 138, 230, 24), "Controller", noteStyle);
        DrawRow(x, y + 181, TrainerAction.Flight);
        DrawRow(x, y + 221, TrainerAction.Teleport);
        Fill(new Rect(x + 18, y + 280, 764, 265), Card);
        GUI.Label(new Rect(x + 30, y + 287, 230, 25), "FLIGHT CONTROLS", sectionStyle);
        int row = 0;
        foreach (var action in TrainerBindings.Actions)
            if (action > TrainerAction.Teleport && action != TrainerAction.TurnLeft && action != TrainerAction.TurnRight)
                DrawRow(x, y + 321 + row++ * 32, action);
        if (message.Length > 0) GUI.Label(new Rect(x + 24, y + 557, 750, 42), message, noteStyle);
        if (Button(new Rect(x + 24, y + 676, 166, 30), "Restore bindings")) { capture = null; Bindings.Reset(); Save(); }
    }
}
