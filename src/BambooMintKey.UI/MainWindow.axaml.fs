#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Diagnostics
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Interactivity
open Avalonia.Input

type MainWindow (args: string[]) as this = 
    inherit Window ()

    let mutable mainTabs: TabControl = null
    let mutable tabAbout: TabItem = null
    let mutable rbTelex: RadioButton = null
    let mutable rbVni: RadioButton = null
    let mutable rbSimpleTelex: RadioButton = null
    let mutable cbCharset: ComboBox = null
    let mutable txtHotkeyDisplay: TextBox = null
    let mutable btnRecordHotkey: Button = null
    let mutable btnClearHotkey: Button = null
    let mutable txtHotkeyHelp: TextBlock = null
    let mutable chipCtrlShift: Button = null
    let mutable chipAltZ: Button = null
    let mutable chipCtrlSpace: Button = null
    let mutable chipCtrlTilde: Button = null
    let mutable chkStartup: CheckBox = null
    let mutable rbToneModern: RadioButton = null
    let mutable rbToneClassic: RadioButton = null
    let mutable chkAutoRestore: CheckBox = null
    let mutable chkRepeatUndo: CheckBox = null
    let mutable chkLeadingW: CheckBox = null
    let mutable txtSandbox: TextBox = null
    let mutable btnClearSandbox: Button = null
    let mutable btnGithub: Button = null
    let mutable btnCheckUpdate: Button = null
    let mutable btnDefault: Button = null
    let mutable btnSave: Button = null
    let mutable txtStatus: TextBlock = null

    let mutable isRecordingHotkey = false
    let mutable currentVKey = 0x10u
    let mutable currentModifiers = 0x0202u
    let mutable currentDisplay = "Ctrl + Shift"

    do
        this.InitializeComponent()
        this.BindControls()
        this.LoadSettings()
        this.HandleCommandLineArgs()

    new() = MainWindow([||])

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    member private this.SetHotkey(vKey: uint32, mods: uint32, ?display: string) =
        currentVKey <- vKey
        currentModifiers <- mods
        currentDisplay <- defaultArg display (ConfigStore.getHotkeyDisplayString vKey mods)
        if txtHotkeyDisplay <> null then
            txtHotkeyDisplay.Text <- currentDisplay
        if isRecordingHotkey then
            isRecordingHotkey <- false
            if btnRecordHotkey <> null then btnRecordHotkey.Content <- "⌨ Bấm để gán phím"
        if txtHotkeyHelp <> null then
            txtHotkeyHelp.Text <- sprintf "Phím tắt hiện tại: %s" currentDisplay

    member private this.BindControls() =
        mainTabs <- this.FindControl<TabControl>("MainTabs")
        tabAbout <- this.FindControl<TabItem>("TabAbout")
        rbTelex <- this.FindControl<RadioButton>("RbTelex")
        rbVni <- this.FindControl<RadioButton>("RbVni")
        rbSimpleTelex <- this.FindControl<RadioButton>("RbSimpleTelex")
        cbCharset <- this.FindControl<ComboBox>("CbCharset")
        txtHotkeyDisplay <- this.FindControl<TextBox>("TxtHotkeyDisplay")
        btnRecordHotkey <- this.FindControl<Button>("BtnRecordHotkey")
        btnClearHotkey <- this.FindControl<Button>("BtnClearHotkey")
        txtHotkeyHelp <- this.FindControl<TextBlock>("TxtHotkeyHelp")
        chipCtrlShift <- this.FindControl<Button>("ChipCtrlShift")
        chipAltZ <- this.FindControl<Button>("ChipAltZ")
        chipCtrlSpace <- this.FindControl<Button>("ChipCtrlSpace")
        chipCtrlTilde <- this.FindControl<Button>("ChipCtrlTilde")
        chkStartup <- this.FindControl<CheckBox>("ChkStartup")
        rbToneModern <- this.FindControl<RadioButton>("RbToneModern")
        rbToneClassic <- this.FindControl<RadioButton>("RbToneClassic")
        chkAutoRestore <- this.FindControl<CheckBox>("ChkAutoRestore")
        chkRepeatUndo <- this.FindControl<CheckBox>("ChkRepeatUndo")
        chkLeadingW <- this.FindControl<CheckBox>("ChkLeadingW")
        txtSandbox <- this.FindControl<TextBox>("TxtSandbox")
        btnClearSandbox <- this.FindControl<Button>("BtnClearSandbox")
        btnGithub <- this.FindControl<Button>("BtnGithub")
        btnCheckUpdate <- this.FindControl<Button>("BtnCheckUpdate")
        btnDefault <- this.FindControl<Button>("BtnDefault")
        btnSave <- this.FindControl<Button>("BtnSave")
        txtStatus <- this.FindControl<TextBlock>("TxtStatus")

        // Hotkey recording actions
        if btnRecordHotkey <> null then
            btnRecordHotkey.Click.Add(fun _ ->
                if not isRecordingHotkey then
                    isRecordingHotkey <- true
                    btnRecordHotkey.Content <- "Hủy gán"
                    if txtHotkeyDisplay <> null then txtHotkeyDisplay.Text <- "Nhấn tổ hợp phím..."
                    if txtHotkeyHelp <> null then txtHotkeyHelp.Text <- "Đang lắng nghe: Hãy nhấn tổ hợp phím bất kỳ trên bàn phím (ví dụ: Alt+Z, Ctrl+Space, F9, ...)"
                else
                    isRecordingHotkey <- false
                    btnRecordHotkey.Content <- "⌨ Bấm để gán phím"
                    if txtHotkeyDisplay <> null then txtHotkeyDisplay.Text <- currentDisplay
                    if txtHotkeyHelp <> null then txtHotkeyHelp.Text <- "Bấm nút 'Gán phím' rồi nhấn tổ hợp phím bất kỳ trên bàn phím của bạn."
            )

        if btnClearHotkey <> null then
            btnClearHotkey.Click.Add(fun _ ->
                this.SetHotkey(0u, 0u, "Không sử dụng phím tắt")
            )

        // Quick presets
        if chipCtrlShift <> null then chipCtrlShift.Click.Add(fun _ -> this.SetHotkey(0x10u, 0x0202u, "Ctrl + Shift"))
        if chipAltZ <> null then chipAltZ.Click.Add(fun _ -> this.SetHotkey(0x5Au, 0x0001u, "Alt + Z"))
        if chipCtrlSpace <> null then chipCtrlSpace.Click.Add(fun _ -> this.SetHotkey(0x20u, 0x0002u, "Ctrl + Space"))
        if chipCtrlTilde <> null then chipCtrlTilde.Click.Add(fun _ -> this.SetHotkey(0xC0u, 0x0002u, "Ctrl + ~"))

        // Key Down Interception for Hotkey Recording
        this.KeyDown.Add(fun e ->
            if isRecordingHotkey then
                if e.Key = Key.Escape then
                    isRecordingHotkey <- false
                    if btnRecordHotkey <> null then btnRecordHotkey.Content <- "⌨ Bấm để gán phím"
                    if txtHotkeyDisplay <> null then txtHotkeyDisplay.Text <- currentDisplay
                    if txtHotkeyHelp <> null then txtHotkeyHelp.Text <- "Đã hủy gán phím."
                    e.Handled <- true
                else
                    // Check active modifiers
                    let hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                    let hasAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                    let hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift)

                    let mutable mods = 0u
                    if hasCtrl then mods <- mods ||| 0x0002u
                    if hasAlt then mods <- mods ||| 0x0001u
                    if hasShift then mods <- mods ||| 0x0004u

                    match e.Key with
                    | Key.LeftCtrl | Key.RightCtrl | Key.LeftAlt | Key.RightAlt | Key.LeftShift | Key.RightShift ->
                        // Nếu chỉ mới nhấn các phím bổ trợ, hiển thị trạng thái chờ và CHƯA kết thúc gán
                        let modParts = System.Collections.Generic.List<string>()
                        if hasCtrl then modParts.Add("Ctrl")
                        if hasAlt then modParts.Add("Alt")
                        if hasShift then modParts.Add("Shift")
                        if modParts.Count > 0 && txtHotkeyDisplay <> null then
                            txtHotkeyDisplay.Text <- sprintf "%s + ..." (String.Join(" + ", modParts))
                        e.Handled <- true

                    | nonModKey ->
                        // Người dùng đã nhấn phím chính (kết hợp cùng 1, 2 hoặc 3 phím bổ trợ Ctrl/Alt/Shift)
                        let mutable vKey = 0u
                        match nonModKey with
                        | Key.Space -> vKey <- 0x20u
                        | Key.OemTilde -> vKey <- 0xC0u
                        | Key.OemPipe -> vKey <- 0xDCu
                        | Key.OemQuestion -> vKey <- 0xBFu
                        | Key.OemOpenBrackets -> vKey <- 0xDBu
                        | Key.OemCloseBrackets -> vKey <- 0xDDu
                        | Key.OemSemicolon -> vKey <- 0xBAu
                        | Key.OemQuotes -> vKey <- 0xDEu
                        | Key.OemComma -> vKey <- 0xBCu
                        | Key.OemPeriod -> vKey <- 0xBEu
                        | Key.OemMinus -> vKey <- 0xBDu
                        | Key.OemPlus -> vKey <- 0xBBu
                        | Key.Tab -> vKey <- 0x09u
                        | Key.Back -> vKey <- 0x08u
                        | Key.Enter -> vKey <- 0x0Du
                        | Key.CapsLock -> vKey <- 0x14u
                        | k when k >= Key.A && k <= Key.Z ->
                            vKey <- uint32 (int k - int Key.A + 0x41)
                        | k when k >= Key.D0 && k <= Key.D9 ->
                            vKey <- uint32 (int k - int Key.D0 + 0x30)
                        | k when k >= Key.NumPad0 && k <= Key.NumPad9 ->
                            vKey <- uint32 (int k - int Key.NumPad0 + 0x60)
                        | k when k >= Key.F1 && k <= Key.F12 ->
                            vKey <- uint32 (int k - int Key.F1 + 0x70)
                        | _ -> ()

                        if vKey <> 0u then
                            this.SetHotkey(vKey, mods)
                            if txtStatus <> null then txtStatus.Text <- sprintf "Đã gán phím tắt mới: %s" currentDisplay
                            e.Handled <- true
        )

        // Key Up Interception (dành riêng khi người dùng chỉ muốn dùng tổ hợp thuần phím bổ trợ như Ctrl + Shift)
        this.KeyUp.Add(fun e ->
            if isRecordingHotkey then
                if (e.Key = Key.LeftShift || e.Key = Key.RightShift) && e.KeyModifiers.HasFlag(KeyModifiers.Control) then
                    this.SetHotkey(0x10u, 0x0202u, "Ctrl + Shift")
                    if txtStatus <> null then txtStatus.Text <- "Đã gán phím tắt mới: Ctrl + Shift"
                elif (e.Key = Key.LeftCtrl || e.Key = Key.RightCtrl) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) then
                    this.SetHotkey(0x10u, 0x0202u, "Ctrl + Shift")
                    if txtStatus <> null then txtStatus.Text <- "Đã gán phím tắt mới: Ctrl + Shift"
                elif (e.Key = Key.LeftShift || e.Key = Key.RightShift) && e.KeyModifiers.HasFlag(KeyModifiers.Alt) then
                    this.SetHotkey(0x10u, 0x0201u, "Alt + Shift")
                    if txtStatus <> null then txtStatus.Text <- "Đã gán phím tắt mới: Alt + Shift"
        )

        if btnClearSandbox <> null then
            btnClearSandbox.Click.Add(fun _ -> 
                if txtSandbox <> null then txtSandbox.Text <- ""
            )

        if btnGithub <> null then
            btnGithub.Click.Add(fun _ ->
                try
                    Process.Start(ProcessStartInfo("https://github.com/thatislg/BambooMintKey", UseShellExecute = true)) |> ignore
                with _ -> ()
            )

        if btnCheckUpdate <> null then
            btnCheckUpdate.Click.Add(fun _ ->
                if txtStatus <> null then
                    txtStatus.Text <- "Bạn đang sử dụng phiên bản mới nhất (v1.0.0)."
            )

        if btnDefault <> null then
            btnDefault.Click.Add(fun _ -> this.ApplyDefaults())

        if btnSave <> null then
            btnSave.Click.Add(fun _ -> this.SaveAndClose())

    member private this.LoadSettings() =
        let cfg = ConfigStore.loadConfig()
        
        if rbTelex <> null && rbVni <> null && rbSimpleTelex <> null then
            match cfg.InputMethod with
            | 1uy -> rbVni.IsChecked <- Nullable true
            | 2uy -> rbSimpleTelex.IsChecked <- Nullable true
            | _ -> rbTelex.IsChecked <- Nullable true

        if cbCharset <> null then
            cbCharset.SelectedIndex <- int cfg.Charset

        this.SetHotkey(cfg.HotkeyVKey, cfg.HotkeyModifiers, cfg.HotkeyDisplay)

        if chkStartup <> null then
            chkStartup.IsChecked <- Nullable cfg.StartWithWindows

        if rbToneModern <> null && rbToneClassic <> null then
            if cfg.ToneStyle = 1uy then
                rbToneClassic.IsChecked <- Nullable true
            else
                rbToneModern.IsChecked <- Nullable true

        if chkAutoRestore <> null then
            chkAutoRestore.IsChecked <- Nullable cfg.AutoRestoreEnglishWords

        if chkRepeatUndo <> null then
            chkRepeatUndo.IsChecked <- Nullable cfg.AllowRepeatKeyUndo

        if chkLeadingW <> null then
            chkLeadingW.IsChecked <- Nullable cfg.AllowLeadingWAsU

    member private this.ApplyDefaults() =
        let def = AppConfig.Default
        if rbTelex <> null then rbTelex.IsChecked <- Nullable true
        if cbCharset <> null then cbCharset.SelectedIndex <- int def.Charset
        this.SetHotkey(def.HotkeyVKey, def.HotkeyModifiers, def.HotkeyDisplay)
        if chkStartup <> null then chkStartup.IsChecked <- Nullable def.StartWithWindows
        if rbToneModern <> null then rbToneModern.IsChecked <- Nullable true
        if chkAutoRestore <> null then chkAutoRestore.IsChecked <- Nullable def.AutoRestoreEnglishWords
        if chkRepeatUndo <> null then chkRepeatUndo.IsChecked <- Nullable def.AllowRepeatKeyUndo
        if chkLeadingW <> null then chkLeadingW.IsChecked <- Nullable def.AllowLeadingWAsU
        if txtStatus <> null then txtStatus.Text <- "Đã khôi phục thiết lập mặc định."

    member private this.HandleCommandLineArgs() =
        let hasAboutArg = 
            args |> Array.exists (fun a -> a.Equals("--about", StringComparison.OrdinalIgnoreCase))
            || Environment.GetCommandLineArgs() |> Array.exists (fun a -> a.Equals("--about", StringComparison.OrdinalIgnoreCase))

        if hasAboutArg && mainTabs <> null && tabAbout <> null then
            mainTabs.SelectedItem <- tabAbout

    member private this.SaveAndClose() =
        let cfg = ConfigStore.loadConfig()

        if rbVni <> null && rbVni.IsChecked = Nullable true then
            cfg.InputMethod <- 1uy
        elif rbSimpleTelex <> null && rbSimpleTelex.IsChecked = Nullable true then
            cfg.InputMethod <- 2uy
        else
            cfg.InputMethod <- 0uy

        if cbCharset <> null then
            cfg.Charset <- byte (Math.Max(0, cbCharset.SelectedIndex))

        cfg.HotkeyVKey <- currentVKey
        cfg.HotkeyModifiers <- currentModifiers
        cfg.HotkeyDisplay <- currentDisplay

        if currentVKey = 0x10u && (currentModifiers &&& 0x0002u <> 0u) then
            cfg.ToggleHotkey <- 0uy
        elif currentVKey = 0x5Au && currentModifiers = 0x0001u then
            cfg.ToggleHotkey <- 1uy
        elif currentVKey = 0x20u && currentModifiers = 0x0002u then
            cfg.ToggleHotkey <- 2uy
        elif currentVKey = 0u && currentModifiers = 0u then
            cfg.ToggleHotkey <- 3uy
        else
            cfg.ToggleHotkey <- 4uy // Custom

        if chkStartup <> null then
            cfg.StartWithWindows <- chkStartup.IsChecked.GetValueOrDefault(false)

        if rbToneClassic <> null && rbToneClassic.IsChecked = Nullable true then
            cfg.ToneStyle <- 1uy
        else
            cfg.ToneStyle <- 0uy

        if chkAutoRestore <> null then
            cfg.AutoRestoreEnglishWords <- chkAutoRestore.IsChecked.GetValueOrDefault(true)

        if chkRepeatUndo <> null then
            cfg.AllowRepeatKeyUndo <- chkRepeatUndo.IsChecked.GetValueOrDefault(true)

        if chkLeadingW <> null then
            cfg.AllowLeadingWAsU <- chkLeadingW.IsChecked.GetValueOrDefault(false)

        ConfigStore.saveConfig(cfg)
        this.Close()
