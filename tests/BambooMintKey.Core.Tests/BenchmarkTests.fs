// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open System
open System.Diagnostics
open Xunit
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Engine

/// <summary>
/// Benchmark & Stress test cho độ trễ gõ phím và độ ổn định (Phase 8 / Milestone 6).
/// Mục tiêu theo 008_05: trung bình < 0.1ms/phím, 99th percentile < 0.5ms.
/// </summary>
module BenchmarkTests =

    /// Chuỗi tham chiếu pha trộn tiếng Việt (có dấu) + tiếng Anh; khoảng trắng dùng để chốt từ.
    let private sampleText = "vua muaw chuaw cuawj duawx gif has post form core start test data"

    /// Chuyển ký tự thành KeyInput: khoảng trắng -> WordBreak (chốt + reset), còn lại -> Char.
    let private toInput (c: char) =
        if c = ' ' then KeyInput.WordBreak ' ' else KeyInput.Char c

    let private typeStream (iterations: int) =
        let config = EngineConfig.Default
        let mutable state = WordState.Empty
        for i in 1 .. iterations do
            let c = sampleText[i % sampleText.Length]
            let s, _ = TelexEngine.processKey state (toInput c) config
            state <- s
        state

    [<Fact>]
    let ``M6.1 - processKey trung bình dưới 0.1ms (100k lượt gõ)`` () =
        let config = EngineConfig.Default
        // Warm-up để lazy-load từ điển (FrozenSet) trước khi đo.
        typeStream 1000 |> ignore

        let mutable state = WordState.Empty
        let n = 100000
        let sw = Stopwatch.StartNew()
        for i in 1 .. n do
            let c = sampleText[i % sampleText.Length]
            let s, _ = TelexEngine.processKey state (toInput c) config
            state <- s
        sw.Stop()

        let perKeyUs = sw.Elapsed.TotalMilliseconds * 1000.0 / float n
        // Ngưỡng: 0.1ms = 100 micro giây (để dư rộng cho môi trường CI; thực tế ~vài µs).
        Assert.True(perKeyUs < 100.0, sprintf "processKey quá chậm: %.2f µs/phím" perKeyUs)

    [<Fact>]
    let ``M6.2 - 100k keystroke không phình bộ nhớ managed (GC ổn định)`` () =
        let before = GC.GetTotalMemory(true)
        typeStream 100000 |> ignore
        let after = GC.GetTotalMemory(true)
        // Không có rò rỉ managed đáng kể: delta < 10MB (đa số là overhead đo, không phải tích lũy).
        let deltaMB = float (after - before) / 1024.0 / 1024.0
        Assert.True(deltaMB < 10.0, sprintf "Bộ nhớ managed tăng bất thường: %.2f MB" deltaMB)
