// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open System
open System.IO
open System.Text
open System.Diagnostics
open System.Collections.Generic
open Xunit
open Xunit.Abstractions
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Engine

type GoldenTestCase = {
    Method: string
    InputKeys: string
    ExpectedOutputs: string list
    Category: string
    Notes: string
    LineNumber: int
}

type CategoryResult = {
    Category: string
    Total: int
    Passed: int
    Failed: int
    TotalKeystrokes: int
    TotalMicroseconds: double
    FailedCases: (GoldenTestCase * string) list
}

module GoldenBenchmarkEngine =

    let rec findRepoRoot (dir: string) : string =
        if File.Exists(Path.Combine(dir, "BambooMintKey.slnx")) || Directory.Exists(Path.Combine(dir, ".git")) then
            dir
        else
            let parent = Directory.GetParent(dir)
            if isNull parent then dir
            else findRepoRoot parent.FullName

    let parseInputTokens (input: string) : KeyInput list =
        let rec loop (chars: char list) (acc: KeyInput list) =
            match chars with
            | [] -> List.rev acc
            | '<' :: 'B' :: 'S' :: '>' :: rest ->
                loop rest (KeyInput.Backspace :: acc)
            | '<' :: 'S' :: 'P' :: 'A' :: 'C' :: 'E' :: '>' :: rest ->
                loop rest (KeyInput.WordBreak ' ' :: acc)
            | ' ' :: rest ->
                loop rest (KeyInput.WordBreak ' ' :: acc)
            | c :: rest ->
                loop rest (KeyInput.Char c :: acc)
        loop (Seq.toList input) []

    let parseGoldenCases (content: string) : GoldenTestCase list =
        let lines = content.Split([| "\r\n"; "\n" |], StringSplitOptions.None)
        let cases = List<GoldenTestCase>()
        for idx = 0 to lines.Length - 1 do
            let rawLine = lines[idx].Trim()
            if not (String.IsNullOrWhiteSpace rawLine) && not (rawLine.StartsWith("#")) then
                let parts = rawLine.Split('|')
                if parts.Length >= 3 then
                    let method = parts[0].Trim()
                    let inputKeys = parts[1].Trim()
                    let expectedRaw = parts[2].Trim()
                    let category = if parts.Length > 3 then parts[3].Trim() else "general"
                    let notes = if parts.Length > 4 then parts[4].Trim() else ""
                    
                    let expectedList =
                        expectedRaw.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.map (fun s -> s.Trim())
                        |> Array.toList

                    cases.Add({
                        Method = method
                        InputKeys = inputKeys
                        ExpectedOutputs = expectedList
                        Category = category
                        Notes = notes
                        LineNumber = idx + 1
                    })
        cases |> Seq.toList

    let runCase (tc: GoldenTestCase) (config: EngineConfig) : string * double =
        let tokens = parseInputTokens tc.InputKeys
        let mutable state = WordState.Empty
        let sb = StringBuilder()
        let sw = Stopwatch.StartNew()
        for token in tokens do
            let newState, action = TelexEngine.processKey state token config
            state <- newState
            match action with
            | EngineAction.Commit committedText ->
                sb.Append(committedText) |> ignore
            | EngineAction.PassThrough ->
                match token with
                | KeyInput.WordBreak c -> sb.Append(c) |> ignore
                | KeyInput.Char c -> sb.Append(c) |> ignore
                | _ -> ()
            | _ -> ()
        sw.Stop()
        let finalOutput = sb.ToString() + state.TransformedText
        let totalUs = sw.Elapsed.TotalMilliseconds * 1000.0
        (finalOutput, totalUs)

    let isMatch (actual: string) (expectedList: string list) : bool =
        expectedList |> List.exists (fun exp -> String.Equals(exp, actual, StringComparison.Ordinal))

    let loadDefaultCases () : GoldenTestCase list =
        let baseDir = AppContext.BaseDirectory
        let repoRoot = findRepoRoot baseDir
        let casesPath = Path.Combine(repoRoot, "docs", "6.Benchmark", "cases", "tier1_golden.cases")
        if File.Exists(casesPath) then
            let content = File.ReadAllText(casesPath, Encoding.UTF8)
            parseGoldenCases content
        else
            failwithf "Không tìm thấy file cases tại: %s" casesPath

    let evaluateSuite (cases: GoldenTestCase list) (config: EngineConfig) : Dictionary<string, CategoryResult> =
        let resultsByCategory = Dictionary<string, CategoryResult>()
        for tc in cases do
            let cat = tc.Category
            if not (resultsByCategory.ContainsKey cat) then
                resultsByCategory.[cat] <- {
                    Category = cat
                    Total = 0
                    Passed = 0
                    Failed = 0
                    TotalKeystrokes = 0
                    TotalMicroseconds = 0.0
                    FailedCases = []
                }

            let actual, us = runCase tc config
            let cur = resultsByCategory.[cat]
            let tokens = parseInputTokens tc.InputKeys
            let passed = isMatch actual tc.ExpectedOutputs

            if passed then
                resultsByCategory.[cat] <- {
                    cur with
                        Total = cur.Total + 1
                        Passed = cur.Passed + 1
                        TotalKeystrokes = cur.TotalKeystrokes + tokens.Length
                        TotalMicroseconds = cur.TotalMicroseconds + us
                }
            else
                resultsByCategory.[cat] <- {
                    cur with
                        Total = cur.Total + 1
                        Failed = cur.Failed + 1
                        TotalKeystrokes = cur.TotalKeystrokes + tokens.Length
                        TotalMicroseconds = cur.TotalMicroseconds + us
                        FailedCases = (tc, actual) :: cur.FailedCases
                }
        resultsByCategory

type GoldenBenchmarkTests (output: ITestOutputHelper) =

    [<Fact>]
    let ``Tier 1 Benchmark - Chạy toàn bộ Golden Test Suite và xuất bảng báo cáo`` () =
        let cases = GoldenBenchmarkEngine.loadDefaultCases ()
        Assert.NotEmpty(cases)

        // Cấu hình thử nghiệm: Bật AllowLeadingWAsU để kiểm thử trọn vẹn tập shortcuts tiếng Việt
        let config = { EngineConfig.Default with AllowLeadingWAsU = true }
        let resultsByCategory = GoldenBenchmarkEngine.evaluateSuite cases config

        // In bảng báo cáo chuẩn định dạng Markdown / Console
        output.WriteLine("=========================================================================================")
        output.WriteLine("           BÁO CÁO BENCHMARK TẦNG 1: BAMBOOMINTKEY ENGINE (WINDOWS / x64)                ")
        output.WriteLine("   Cấu hình: Telex | ToneStyle: Modern/Traditional Dual | AllowLeadingWAsU: True         ")
        output.WriteLine("=========================================================================================")
        output.WriteLine(sprintf "%-18s | %-7s | %-7s | %-7s | %-9s | %-12s" "Nhóm Kiểm Thử" "Tổng" "Đạt" "Hỏng" "Tỷ Lệ %" "Độ Trễ/Phím")
        output.WriteLine("-------------------+---------+---------+---------+-----------+-----------------------------")

        let mutable grandTotal = 0
        let mutable grandPassed = 0
        let mutable grandFailed = 0
        let mutable grandKeys = 0
        let mutable grandUs = 0.0

        for kvp in resultsByCategory do
            let res = kvp.Value
            grandTotal <- grandTotal + res.Total
            grandPassed <- grandPassed + res.Passed
            grandFailed <- grandFailed + res.Failed
            grandKeys <- grandKeys + res.TotalKeystrokes
            grandUs <- grandUs + res.TotalMicroseconds

            let rate = (float res.Passed / float res.Total) * 100.0
            let avgUsPerKey = if res.TotalKeystrokes > 0 then res.TotalMicroseconds / float res.TotalKeystrokes else 0.0
            output.WriteLine(sprintf "%-18s | %-7d | %-7d | %-7d | %8.1f%% | %8.2f µs" res.Category res.Total res.Passed res.Failed rate avgUsPerKey)

        output.WriteLine("-------------------+---------+---------+---------+-----------+-----------------------------")
        let grandRate = if grandTotal > 0 then (float grandPassed / float grandTotal) * 100.0 else 0.0
        let grandAvgUs = if grandKeys > 0 then grandUs / float grandKeys else 0.0
        output.WriteLine(sprintf "%-18s | %-7d | %-7d | %-7d | %8.1f%% | %8.2f µs" "TỔNG CỘNG" grandTotal grandPassed grandFailed grandRate grandAvgUs)
        output.WriteLine("=========================================================================================")

        // In chi tiết thất bại nếu có
        for kvp in resultsByCategory do
            let res = kvp.Value
            if not res.FailedCases.IsEmpty then
                output.WriteLine(sprintf "\n[Chi tiết lỗi nhóm: %s]" res.Category)
                for (tc, actual) in List.rev res.FailedCases do
                    let expectedStr = String.Join(" HOẶC ", tc.ExpectedOutputs)
                    output.WriteLine(sprintf "  - Dòng %d: phím='%s' -> mong đợi='%s', thực tế='%s' (%s)" 
                        tc.LineNumber tc.InputKeys expectedStr actual tc.Notes)

        Assert.Equal(0, grandFailed)
