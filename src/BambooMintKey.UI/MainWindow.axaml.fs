#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Diagnostics
open System.Reflection
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Interactivity
open Avalonia.Input
open Avalonia.Threading

type MainWindow (args: string[]) as this = 
    inherit Window ()

    let mutable mainTabs: TabControl = null
    let mutable tabAbout: TabItem = null
    let mutable rbTelex: RadioButton = null
    let mutable rbVni: RadioButton = null
    let mutable rbSimpleTelex: RadioButton = null
    let mutable cbCharset: ComboBox = null
    let mutable chkStartup: CheckBox = null
    let mutable rbToneModern: RadioButton = null
    let mutable rbToneClassic: RadioButton = null
    let mutable chkAutoRestore: CheckBox = null
    let mutable chkRepeatUndo: CheckBox = null
    let mutable chkLeadingW: CheckBox = null
    let mutable chkFreeTone: CheckBox = null
    let mutable chkPreedit: CheckBox = null
    let mutable txtSandbox: TextBox = null
    let mutable btnClearSandbox: Button = null
    let mutable btnGithub: Button = null
    let mutable btnCheckUpdate: Button = null
    let mutable btnDefault: Button = null
    let mutable btnSave: Button = null
    let mutable txtStatus: TextBlock = null

    let mutable syncTimer: DispatcherTimer = null
    let mutable lastKnownSeq = 0u
    let mutable isUpdatingFromSync = false

    let fixedVKey = 0xC0u
    let fixedModifiers = 0x0001u
    let fixedDisplay = "`/ ~ / 半角/全角 (JP) | Alt + ~"

    do
        this.InitializeComponent()
        this.BindControls()
        this.LoadSettings()
        this.HandleCommandLineArgs()

    new() = MainWindow([||])

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    member private this.BindControls() =
        mainTabs <- this.FindControl<TabControl>("MainTabs")
        tabAbout <- this.FindControl<TabItem>("TabAbout")
        rbTelex <- this.FindControl<RadioButton>("RbTelex")
        rbVni <- this.FindControl<RadioButton>("RbVni")
        rbSimpleTelex <- this.FindControl<RadioButton>("RbSimpleTelex")
        cbCharset <- this.FindControl<ComboBox>("CbCharset")
        chkStartup <- this.FindControl<CheckBox>("ChkStartup")
        rbToneModern <- this.FindControl<RadioButton>("RbToneModern")
        rbToneClassic <- this.FindControl<RadioButton>("RbToneClassic")
        chkAutoRestore <- this.FindControl<CheckBox>("ChkAutoRestore")
        chkRepeatUndo <- this.FindControl<CheckBox>("ChkRepeatUndo")
        chkLeadingW <- this.FindControl<CheckBox>("ChkLeadingW")
        chkFreeTone <- this.FindControl<CheckBox>("ChkFreeTone")
        chkPreedit <- this.FindControl<CheckBox>("ChkPreedit")
        txtSandbox <- this.FindControl<TextBox>("TxtSandbox")
        btnClearSandbox <- this.FindControl<Button>("BtnClearSandbox")
        btnGithub <- this.FindControl<Button>("BtnGithub")
        btnCheckUpdate <- this.FindControl<Button>("BtnCheckUpdate")
        btnDefault <- this.FindControl<Button>("BtnDefault")
        btnSave <- this.FindControl<Button>("BtnSave")
        txtStatus <- this.FindControl<TextBlock>("TxtStatus")

        let txtVersionInfo = this.FindControl<TextBlock>("TxtVersionInfo")
        let currentVersionStr =
            try
                let procPath = Environment.ProcessPath
                if not (String.IsNullOrEmpty(procPath)) then
                    let fvi = FileVersionInfo.GetVersionInfo(procPath)
                    if not (String.IsNullOrEmpty(fvi.ProductVersion)) then
                        let cleanVer = fvi.ProductVersion.Split('+').[0].Trim()
                        cleanVer
                    elif not (String.IsNullOrEmpty(fvi.FileVersion)) then
                        let parts = fvi.FileVersion.Split('.')
                        if parts.Length >= 3 then sprintf "%s.%s.%s" parts.[0] parts.[1] parts.[2] else fvi.FileVersion
                    else
                        let ver = Assembly.GetExecutingAssembly().GetName().Version
                        if ver <> null then sprintf "%d.%d.%d" ver.Major ver.Minor (max 0 ver.Build) else "1.0.1"
                else
                    let ver = Assembly.GetExecutingAssembly().GetName().Version
                    if ver <> null then sprintf "%d.%d.%d" ver.Major ver.Minor (max 0 ver.Build) else "1.0.1"
            with _ -> "1.0.1"

        if txtVersionInfo <> null then
            txtVersionInfo.Text <- sprintf "Phiên bản %s (NativeAOT & Pure F# Core)" currentVersionStr

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
                    txtStatus.Text <- sprintf "Bạn đang sử dụng phiên bản mới nhất (v%s)." currentVersionStr
            )

        if btnDefault <> null then
            btnDefault.Click.Add(fun _ -> this.ApplyDefaults())

        if btnSave <> null then
            btnSave.Click.Add(fun _ -> this.SaveAndClose())

        // Tự động đồng bộ 2 chiều ngay lập tức với Shared Memory khi người dùng thay đổi bất kỳ tùy chọn nào
        let onSettingChanged (_: obj) =
            this.AutoSyncToShared()

        if chkFreeTone <> null then chkFreeTone.IsCheckedChanged.Add(onSettingChanged)
        if chkPreedit <> null then chkPreedit.IsCheckedChanged.Add(onSettingChanged)
        if chkAutoRestore <> null then chkAutoRestore.IsCheckedChanged.Add(onSettingChanged)
        if chkRepeatUndo <> null then chkRepeatUndo.IsCheckedChanged.Add(onSettingChanged)
        if chkLeadingW <> null then chkLeadingW.IsCheckedChanged.Add(onSettingChanged)
        if rbToneModern <> null then rbToneModern.IsCheckedChanged.Add(onSettingChanged)
        if rbToneClassic <> null then rbToneClassic.IsCheckedChanged.Add(onSettingChanged)
        if rbTelex <> null then rbTelex.IsCheckedChanged.Add(onSettingChanged)
        if rbVni <> null then rbVni.IsCheckedChanged.Add(onSettingChanged)
        if rbSimpleTelex <> null then rbSimpleTelex.IsCheckedChanged.Add(onSettingChanged)
        if cbCharset <> null then cbCharset.SelectionChanged.Add(onSettingChanged)

        // Khởi tạo timer đồng bộ thời gian thực từ Language Bar (Taskbar Menu) sang Bảng điều khiển
        syncTimer <- new DispatcherTimer()
        syncTimer.Interval <- TimeSpan.FromMilliseconds(150.0)
        syncTimer.Tick.Add(fun _ ->
            let currentSeq = ConfigStore.getStateSequence()
            if currentSeq <> 0u && currentSeq <> lastKnownSeq then
                lastKnownSeq <- currentSeq
                this.LoadSettings()
        )
        syncTimer.Start()

        this.Closed.Add(fun _ ->
            if syncTimer <> null then syncTimer.Stop()
        )

    member private this.AutoSyncToShared() =
        if not isUpdatingFromSync then
            let cfg = ConfigStore.loadConfig()
            if rbVni <> null && rbVni.IsChecked = Nullable true then cfg.InputMethod <- 1uy
            elif rbSimpleTelex <> null && rbSimpleTelex.IsChecked = Nullable true then cfg.InputMethod <- 2uy
            else cfg.InputMethod <- 0uy

            if cbCharset <> null then cfg.Charset <- byte (Math.Max(0, cbCharset.SelectedIndex))

            if rbToneClassic <> null && rbToneClassic.IsChecked = Nullable true then cfg.ToneStyle <- 1uy
            else cfg.ToneStyle <- 0uy

            if chkAutoRestore <> null then cfg.AutoRestoreEnglishWords <- chkAutoRestore.IsChecked.GetValueOrDefault(true)
            if chkRepeatUndo <> null then cfg.AllowRepeatKeyUndo <- chkRepeatUndo.IsChecked.GetValueOrDefault(true)
            if chkLeadingW <> null then cfg.AllowLeadingWAsU <- chkLeadingW.IsChecked.GetValueOrDefault(false)
            if chkFreeTone <> null then cfg.AllowFreeTonePlacement <- chkFreeTone.IsChecked.GetValueOrDefault(true)
            if chkPreedit <> null then cfg.EnablePreedit <- chkPreedit.IsChecked.GetValueOrDefault(false)

            cfg.HotkeyVKey <- fixedVKey
            cfg.HotkeyModifiers <- fixedModifiers
            cfg.HotkeyDisplay <- fixedDisplay
            cfg.ToggleHotkey <- 0uy

            ConfigStore.saveConfig(cfg)
            lastKnownSeq <- ConfigStore.getStateSequence()

    member private this.LoadSettings() =
        isUpdatingFromSync <- true
        try
            let cfg = ConfigStore.loadConfig()
            
            if rbTelex <> null && rbVni <> null && rbSimpleTelex <> null then
                match cfg.InputMethod with
                | 1uy -> rbVni.IsChecked <- Nullable true
                | 2uy -> rbSimpleTelex.IsChecked <- Nullable true
                | _ -> rbTelex.IsChecked <- Nullable true

            if cbCharset <> null then
                cbCharset.SelectedIndex <- int cfg.Charset

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

            if chkFreeTone <> null then
                chkFreeTone.IsChecked <- Nullable cfg.AllowFreeTonePlacement

            if chkPreedit <> null then
                chkPreedit.IsChecked <- Nullable cfg.EnablePreedit

            lastKnownSeq <- ConfigStore.getStateSequence()
        finally
            isUpdatingFromSync <- false

    member private this.ApplyDefaults() =
        let def = AppConfig.Default
        if rbTelex <> null then rbTelex.IsChecked <- Nullable true
        if cbCharset <> null then cbCharset.SelectedIndex <- int def.Charset
        if chkStartup <> null then chkStartup.IsChecked <- Nullable def.StartWithWindows
        if rbToneModern <> null then rbToneModern.IsChecked <- Nullable true
        if chkAutoRestore <> null then chkAutoRestore.IsChecked <- Nullable def.AutoRestoreEnglishWords
        if chkRepeatUndo <> null then chkRepeatUndo.IsChecked <- Nullable def.AllowRepeatKeyUndo
        if chkLeadingW <> null then chkLeadingW.IsChecked <- Nullable def.AllowLeadingWAsU
        if chkFreeTone <> null then chkFreeTone.IsChecked <- Nullable def.AllowFreeTonePlacement
        if chkPreedit <> null then chkPreedit.IsChecked <- Nullable def.EnablePreedit
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

        cfg.HotkeyVKey <- fixedVKey
        cfg.HotkeyModifiers <- fixedModifiers
        cfg.HotkeyDisplay <- fixedDisplay
        cfg.ToggleHotkey <- 0uy

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

        if chkFreeTone <> null then
            cfg.AllowFreeTonePlacement <- chkFreeTone.IsChecked.GetValueOrDefault(true)

        if chkPreedit <> null then
            cfg.EnablePreedit <- chkPreedit.IsChecked.GetValueOrDefault(false)

        ConfigStore.saveConfig(cfg)
        this.Close()
