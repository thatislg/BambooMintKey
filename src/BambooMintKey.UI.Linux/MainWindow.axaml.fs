// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI.Linux

open System
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Markup.Xaml
open Avalonia.Threading
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Engine

type MainWindow() as this =
    inherit Window()

    // ---- Controls ----
    let mutable chkVietnameseMode : CheckBox = null
    let mutable rbTelex : RadioButton = null
    let mutable rbVni : RadioButton = null
    let mutable rbSimpleTelex : RadioButton = null
    let mutable cbCharset : ComboBox = null
    let mutable rbToneModern : RadioButton = null
    let mutable rbToneClassic : RadioButton = null
    let mutable chkFreeTone : CheckBox = null
    let mutable chkAutoRestore : CheckBox = null
    let mutable chkRepeatUndo : CheckBox = null
    let mutable chkLeadingW : CheckBox = null
    let mutable txtMacroKey : TextBox = null
    let mutable txtMacroValue : TextBox = null
    let mutable lstMacros : ListBox = null
    let mutable txtSandbox : TextBox = null
    let mutable txtStatus : TextBlock = null
    let mutable txtMode : TextBlock = null
    let mutable btnSave : Button = null

    // ---- State ----
    let mutable cfg = ConfigStore.loadConfig()
    let mutable isSyncing = false
    let mutable localState = WordState.Empty
    let mutable committedText = ""
    let syncTimer = DispatcherTimer()

    do
        this.InitializeComponent()
        this.BindControls()
        this.LoadSettings()
        this.StartSyncTimer()

    member private this.InitializeComponent() = AvaloniaXamlLoader.Load(this)

    /// Build EngineConfig từ trạng thái các control trên UI.
    member private this.BuildEngineConfig() : EngineConfig =
        let toneStyle =
            if rbToneClassic <> null && rbToneClassic.IsChecked = Nullable true then
                TonePlacementStyle.Traditional
            else
                TonePlacementStyle.Modern
        { IsEnabled = true
          AutoRestoreEnglishWords = if chkAutoRestore <> null then chkAutoRestore.IsChecked.GetValueOrDefault(true) else true
          AllowRepeatKeyUndo = if chkRepeatUndo <> null then chkRepeatUndo.IsChecked.GetValueOrDefault(true) else true
          AllowLeadingWAsU = if chkLeadingW <> null then chkLeadingW.IsChecked.GetValueOrDefault(false) else false
          ToneStyle = toneStyle
          AllowFreeTonePlacement = if chkFreeTone <> null then chkFreeTone.IsChecked.GetValueOrDefault(true) else true }

    member private this.BindControls() =
        chkVietnameseMode <- this.FindControl<CheckBox>("ChkVietnameseMode")
        rbTelex <- this.FindControl<RadioButton>("RbTelex")
        rbVni <- this.FindControl<RadioButton>("RbVni")
        rbSimpleTelex <- this.FindControl<RadioButton>("RbSimpleTelex")
        cbCharset <- this.FindControl<ComboBox>("CbCharset")
        rbToneModern <- this.FindControl<RadioButton>("RbToneModern")
        rbToneClassic <- this.FindControl<RadioButton>("RbToneClassic")
        chkFreeTone <- this.FindControl<CheckBox>("ChkFreeTone")
        chkAutoRestore <- this.FindControl<CheckBox>("ChkAutoRestore")
        chkRepeatUndo <- this.FindControl<CheckBox>("ChkRepeatUndo")
        chkLeadingW <- this.FindControl<CheckBox>("ChkLeadingW")
        txtMacroKey <- this.FindControl<TextBox>("TxtMacroKey")
        txtMacroValue <- this.FindControl<TextBox>("TxtMacroValue")
        lstMacros <- this.FindControl<ListBox>("LstMacros")
        txtSandbox <- this.FindControl<TextBox>("TxtSandbox")
        txtStatus <- this.FindControl<TextBlock>("TxtStatus")
        txtMode <- this.FindControl<TextBlock>("TxtMode")
        btnSave <- this.FindControl<Button>("BtnSave")

        let btnDefault = this.FindControl<Button>("BtnDefault")
        let btnClearSandbox = this.FindControl<Button>("BtnClearSandbox")
        let btnAddMacro = this.FindControl<Button>("BtnAddMacro")
        let btnRemoveMacro = this.FindControl<Button>("BtnRemoveMacro")
        let txtVersion = this.FindControl<TextBlock>("TxtVersionInfo")

        if txtVersion <> null then
            let ver = Reflection.Assembly.GetExecutingAssembly().GetName().Version
            txtVersion.Text <- sprintf "Phiên bản %d.%d.%d (Fcitx5 Addon + F# Core)" ver.Major ver.Minor (max 0 ver.Build)

        // Toggle V/E -> gọi D-Bus SetVietnameseMode.
        if chkVietnameseMode <> null then
            chkVietnameseMode.IsCheckedChanged.Add(fun _ ->
                if not isSyncing then
                    let enabled = chkVietnameseMode.IsChecked.GetValueOrDefault(true)
                    this.UpdateModeBadge(enabled)
                    Async.Start(async { DbusClient.setVietnameseMode(enabled) }))

        if chkFreeTone <> null then chkFreeTone.IsCheckedChanged.Add(fun _ -> this.MarkDirty())
        if chkAutoRestore <> null then chkAutoRestore.IsCheckedChanged.Add(fun _ -> this.MarkDirty())
        if chkRepeatUndo <> null then chkRepeatUndo.IsCheckedChanged.Add(fun _ -> this.MarkDirty())
        if chkLeadingW <> null then chkLeadingW.IsCheckedChanged.Add(fun _ -> this.MarkDirty())

        if btnClearSandbox <> null then
            btnClearSandbox.Click.Add(fun _ ->
                txtSandbox.Text <- ""
                localState <- WordState.Empty
                committedText <- "")

        if btnAddMacro <> null then btnAddMacro.Click.Add(fun _ -> this.AddMacro())
        if btnRemoveMacro <> null then btnRemoveMacro.Click.Add(fun _ -> this.RemoveMacro())
        if btnDefault <> null then btnDefault.Click.Add(fun _ -> this.ApplyDefaults())
        if btnSave <> null then btnSave.Click.Add(fun _ -> this.SaveAndClose())

        // Sandbox: xử lý phím qua F# Core.
        if txtSandbox <> null then
            txtSandbox.TextInput.Add(fun (e: TextInputEventArgs) ->
                let c = if String.IsNullOrEmpty(e.Text) then '\000' else e.Text.[0]
                if c >= ' ' && c <= '~' then
                    this.SandboxChar(c)
                    e.Handled <- true)
            txtSandbox.KeyDown.Add(fun (e: KeyEventArgs) ->
                match e.Key with
                | Key.Back ->
                    this.SandboxBackspace()
                    e.Handled <- true
                | _ -> ())

    member private this.MarkDirty() =
        if txtStatus <> null then txtStatus.Text <- "Có thay đổi chưa lưu."

    member private this.UpdateModeBadge(enabled: bool) =
        if txtMode <> null then
            txtMode.Text <- if enabled then "● V" else "● E"

    member private this.LoadSettings() =
        isSyncing <- true
        try
            if chkVietnameseMode <> null then chkVietnameseMode.IsChecked <- Nullable cfg.IsVietnameseMode
            if rbTelex <> null && rbVni <> null && rbSimpleTelex <> null then
                match cfg.InputMethod with
                | 1 -> rbVni.IsChecked <- Nullable true
                | 2 -> rbSimpleTelex.IsChecked <- Nullable true
                | _ -> rbTelex.IsChecked <- Nullable true
            if cbCharset <> null then cbCharset.SelectedIndex <- cfg.Charset
            if rbToneModern <> null && rbToneClassic <> null then
                if cfg.ToneStyle = 1 then rbToneClassic.IsChecked <- Nullable true
                else rbToneModern.IsChecked <- Nullable true
            if chkFreeTone <> null then chkFreeTone.IsChecked <- Nullable cfg.AllowFreeTonePlacement
            if chkAutoRestore <> null then chkAutoRestore.IsChecked <- Nullable cfg.AutoRestoreEnglishWords
            if chkRepeatUndo <> null then chkRepeatUndo.IsChecked <- Nullable cfg.AllowRepeatKeyUndo
            if chkLeadingW <> null then chkLeadingW.IsChecked <- Nullable cfg.AllowLeadingWAsU
            this.UpdateModeBadge(cfg.IsVietnameseMode)
            this.RefreshMacroList()
        finally
            isSyncing <- false

    /// Polling trạng thái V/E từ addon (thay cho lắng nghe signal D-Bus).
    member private this.StartSyncTimer() =
        syncTimer.Interval <- TimeSpan.FromMilliseconds(500.0)
        syncTimer.Tick.Add(fun _ ->
            Async.Start(async {
                let v = DbusClient.getVietnameseMode()
                Dispatcher.UIThread.Post(fun () ->
                    if v <> cfg.IsVietnameseMode then
                        cfg.IsVietnameseMode <- v
                        isSyncing <- true
                        if chkVietnameseMode <> null then chkVietnameseMode.IsChecked <- Nullable v
                        this.UpdateModeBadge(v)
                        isSyncing <- false)
            }))
        syncTimer.Start()

    member private this.ApplyDefaults() =
        cfg <- AppConfig()
        this.LoadSettings()
        if txtStatus <> null then txtStatus.Text <- "Đã khôi phục mặc định."

    member private this.RefreshMacroList() =
        if lstMacros <> null then
            lstMacros.Items.Clear()
            for KeyValue(k, v) in cfg.Macros do
                lstMacros.Items.Add(sprintf "%s → %s" k v) |> ignore

    member private this.AddMacro() =
        if txtMacroKey <> null && txtMacroValue <> null then
            let k = txtMacroKey.Text
            let v = txtMacroValue.Text
            if not (String.IsNullOrWhiteSpace(k)) && not (String.IsNullOrWhiteSpace(v)) then
                cfg.Macros.[k] <- v
                this.RefreshMacroList()
                txtMacroKey.Text <- ""
                txtMacroValue.Text <- ""
                this.MarkDirty()

    member private this.RemoveMacro() =
        if lstMacros <> null && lstMacros.SelectedItem <> null then
            let item = string lstMacros.SelectedItem
            let idx = item.IndexOf(" → ")
            if idx > 0 then
                let k = item.Substring(0, idx)
                cfg.Macros.Remove(k) |> ignore
                this.RefreshMacroList()
                this.MarkDirty()

    // ---- Sandbox (gõ thử nghiệm) ----
    member private this.SandboxChar(c: char) =
        let config = this.BuildEngineConfig()
        let newState, action = TelexEngine.processKey localState (KeyInput.Char c) config
        localState <- newState
        match action with
        | EngineAction.UpdateComposition text -> this.UpdateSandbox(text)
        | EngineAction.Commit text ->
            committedText <- committedText + text
            localState <- WordState.Empty
            this.UpdateSandbox("")
        | EngineAction.PassThrough -> ()

    member private this.SandboxBackspace() =
        let config = this.BuildEngineConfig()
        let newState, action = TelexEngine.processKey localState KeyInput.Backspace config
        localState <- newState
        match action with
        | EngineAction.UpdateComposition text -> this.UpdateSandbox(text)
        | _ -> ()

    member private this.UpdateSandbox(preedit: string) =
        if txtSandbox <> null then
            txtSandbox.Text <- committedText + preedit
            txtSandbox.CaretIndex <- txtSandbox.Text.Length

    member private this.SaveAndClose() =
        if chkVietnameseMode <> null then cfg.IsVietnameseMode <- chkVietnameseMode.IsChecked.GetValueOrDefault(true)
        if rbVni <> null && rbVni.IsChecked = Nullable true then cfg.InputMethod <- 1
        elif rbSimpleTelex <> null && rbSimpleTelex.IsChecked = Nullable true then cfg.InputMethod <- 2
        else cfg.InputMethod <- 0
        if cbCharset <> null then cfg.Charset <- max 0 cbCharset.SelectedIndex
        if rbToneClassic <> null && rbToneClassic.IsChecked = Nullable true then cfg.ToneStyle <- 1 else cfg.ToneStyle <- 0
        if chkFreeTone <> null then cfg.AllowFreeTonePlacement <- chkFreeTone.IsChecked.GetValueOrDefault(true)
        if chkAutoRestore <> null then cfg.AutoRestoreEnglishWords <- chkAutoRestore.IsChecked.GetValueOrDefault(true)
        if chkRepeatUndo <> null then cfg.AllowRepeatKeyUndo <- chkRepeatUndo.IsChecked.GetValueOrDefault(true)
        if chkLeadingW <> null then cfg.AllowLeadingWAsU <- chkLeadingW.IsChecked.GetValueOrDefault(false)

        ConfigStore.saveConfig(cfg)
        this.Close()

    /// Đưa cửa sổ lên trước (dùng khi nhận SHOW_WINDOW từ instance mới).
    member this.BringToFront() =
        Dispatcher.UIThread.Post(fun () ->
            if this.WindowState = WindowState.Minimized then this.WindowState <- WindowState.Normal
            this.Activate())
