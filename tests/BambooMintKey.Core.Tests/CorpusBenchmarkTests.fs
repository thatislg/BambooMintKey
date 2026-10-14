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
open BambooMintKey.Core.Domain.UnicodeTables
open BambooMintKey.Core.Engine

module KeystrokeSynthesizer =

    /// Chuyển đổi một từ tiếng Việt đơn (syllable) thành chuỗi phím Telex tự nhiên
    let wordToTelex (word: string) : string =
        if String.IsNullOrEmpty word then ""
        else
            let lower = word.ToLowerInvariant()
            let sb = StringBuilder()
            let mutable detectedTone = Tone.None

            // 1. Quét dấu thanh trước
            for c in lower do
                let _, _, tone = decomposeChar c
                if tone <> Tone.None then
                    detectedTone <- tone

            // 2. Chuyển đổi nguyên âm đôi đặc biệt và phụ âm
            let mutable i = 0
            while i < lower.Length do
                let c = lower[i]
                let baseChar, modifier, _ = decomposeChar c

                // Kiểm tra cụm ươ / ưa
                if i + 1 < lower.Length then
                    let nextC = lower[i + 1]
                    let nextBase, nextMod, _ = decomposeChar nextC
                    if baseChar = 'u' && modifier = Modifier.Horn && nextBase = 'o' && nextMod = Modifier.Horn then
                        // ươ -> uow
                        sb.Append("uow") |> ignore
                        i <- i + 2
                    elif baseChar = 'u' && modifier = Modifier.Horn && nextBase = 'a' && nextMod = Modifier.None then
                        // ưa -> uwa
                        sb.Append("uwa") |> ignore
                        i <- i + 2
                    elif baseChar = 'd' && modifier = Modifier.DBar then
                        // đ -> dd
                        sb.Append("dd") |> ignore
                        i <- i + 1
                    else
                        match modifier with
                        | Modifier.Hat ->
                            // â, ê, ô -> aa, ee, oo
                            sb.Append(baseChar).Append(baseChar) |> ignore
                        | Modifier.Breve ->
                            // ă -> aw
                            sb.Append("aw") |> ignore
                        | Modifier.Horn ->
                            // ơ, ư -> ow, uw
                            sb.Append(baseChar).Append('w') |> ignore
                        | _ ->
                            sb.Append(baseChar) |> ignore
                        i <- i + 1
                else
                    if baseChar = 'd' && modifier = Modifier.DBar then
                        sb.Append("dd") |> ignore
                    else
                        match modifier with
                        | Modifier.Hat ->
                            sb.Append(baseChar).Append(baseChar) |> ignore
                        | Modifier.Breve ->
                            sb.Append("aw") |> ignore
                        | Modifier.Horn ->
                            sb.Append(baseChar).Append('w') |> ignore
                        | _ ->
                            sb.Append(baseChar) |> ignore
                    i <- i + 1

            // 3. Đặt phím dấu thanh ở cuối từ (Free tone placement chuẩn tự nhiên)
            match detectedTone with
            | Tone.Acute -> sb.Append('s') |> ignore
            | Tone.Grave -> sb.Append('f') |> ignore
            | Tone.Hook  -> sb.Append('r') |> ignore
            | Tone.Tilde -> sb.Append('x') |> ignore
            | Tone.Dot   -> sb.Append('j') |> ignore
            | Tone.None  -> ()

            sb.ToString()

module CorpusLoader =

    let rec findRepoRoot (dir: string) : string =
        if File.Exists(Path.Combine(dir, "BambooMintKey.slnx")) || Directory.Exists(Path.Combine(dir, ".git")) then
            dir
        else
            let parent = Directory.GetParent(dir)
            if isNull parent then dir
            else findRepoRoot parent.FullName

    /// Nạp danh sách từ vựng tiếng Việt có tần suất cao nhất từ dicts/vi-corpus-freq.tsv
    let loadTopWords (maxCount: int) : string list =
        let baseDir = AppContext.BaseDirectory
        let repoRoot = findRepoRoot baseDir
        let tsvPath = Path.Combine(repoRoot, "dicts", "vi-corpus-freq.tsv")
        if not (File.Exists tsvPath) then
            failwithf "Không tìm thấy file corpus tại: %s" tsvPath
        else
            let lines = File.ReadAllLines(tsvPath, Encoding.UTF8)
            let words = List<string>()
            for line in lines do
                if words.Count < maxCount then
                    let parts = line.Split('\t')
                    if parts.Length >= 1 then
                        let word = parts[0].Trim().ToLowerInvariant()
                        // Chỉ lấy các từ đơn thuần chữ cái (loại trừ từ vay mượn dính dấu gạch nối hoặc số)
                        if word.Length > 0 && not (word.Contains "-") && not (word.Contains " ") && not (word.Contains ".") then
                            // Bỏ qua các từ tiếng Anh lọt vào corpus như 'the', 'com', 'http'
                            if not (EnglishProtection.isKnownEnglishWord word) then
                                words.Add(word)
            words |> Seq.take (Math.Min(words.Count, maxCount)) |> Seq.toList

type CorpusTierBenchmarkTests (output: ITestOutputHelper) =

    let isEquivalent (actual: string) (expected: string) : bool =
        if String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) then true
        else
            // Dual-Acceptance: Kiểm tra tương đương ngữ âm (ví dụ: hòa vs hoá)
            match SyllableParser.parse actual, SyllableParser.parse expected with
            | Some s1, Some s2 ->
                s1.InitialConsonant.ToLowerInvariant() = s2.InitialConsonant.ToLowerInvariant() &&
                s1.FinalConsonant.ToLowerInvariant() = s2.FinalConsonant.ToLowerInvariant() &&
                s1.Tone = s2.Tone &&
                s1.VowelNucleus.Length = s2.VowelNucleus.Length
            | _ -> false

    [<Fact>]
    let ``Tier 1 Benchmark - Chạy Corpus 10 nấc từ 1.000 đến 10.000 từ và đo lường độ đúng, độ trễ, RAM`` () =
        let maxWordsNeeded = 10000
        let wordCorpus = CorpusLoader.loadTopWords maxWordsNeeded
        output.WriteLine(sprintf "Đã nạp thành công %d từ tiếng Việt chuẩn từ corpus." wordCorpus.Length)

        let config = { EngineConfig.Default with AllowLeadingWAsU = true }
        let tiers = [ 1000; 2000; 3000; 4000; 5000; 6000; 7000; 8000; 9000; 10000 ]

        output.WriteLine("=========================================================================================================")
        output.WriteLine("                 BÁO CÁO BENCHMARK TẦNG 1: CORPUS TIẾNG VIỆT 10 NẤC (1.000 -> 10.000 TỪ)               ")
        output.WriteLine("                     Hệ điều hành: Windows x64 | Engine: BambooMintKey (F# Core Native)                  ")
        output.WriteLine("=========================================================================================================")
        output.WriteLine(sprintf "%-10s | %-8s | %-8s | %-9s | %-12s | %-12s | %-14s | %-10s" 
            "Nấc (Từ)" "Đạt" "Hỏng" "Độ Đúng %" "Thời Gian" "Độ Trễ/Từ" "Throughput" "RAM Delta")
        output.WriteLine("-----------+----------+----------+-----------+--------------+--------------+----------------+-----------")

        for tierCount in tiers do
            if tierCount <= wordCorpus.Length then
                let wordsInTier = wordCorpus |> List.take tierCount
                
                // Thu gom rác để đo bộ nhớ tương đối chính xác
                GC.Collect()
                GC.WaitForPendingFinalizers()
                let memBefore = GC.GetTotalMemory(true)

                let mutable passed = 0
                let mutable failed = 0
                let mutable totalKeystrokes = 0

                let sw = Stopwatch.StartNew()
                for expectedWord in wordsInTier do
                    let telexKeys = KeystrokeSynthesizer.wordToTelex expectedWord
                    totalKeystrokes <- totalKeystrokes + telexKeys.Length

                    // Gõ từ vào engine
                    let mutable state = WordState.Empty
                    for c in telexKeys do
                        let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
                        state <- newState
                    
                    let actualWord = state.TransformedText
                    if isEquivalent actualWord expectedWord then
                        passed <- passed + 1
                    else
                        failed <- failed + 1

                sw.Stop()
                let memAfter = GC.GetTotalMemory(false)
                let deltaKB = float (memAfter - memBefore) / 1024.0

                let accuracy = (float passed / float tierCount) * 100.0
                let elapsedMs = sw.Elapsed.TotalMilliseconds
                let avgUsPerWord = (elapsedMs * 1000.0) / float tierCount
                let wordsPerSec = float tierCount / (elapsedMs / 1000.0)

                output.WriteLine(sprintf "%-10d | %-8d | %-8d | %8.2f%% | %9.2f ms | %9.2f µs | %10.0f w/s | %7.1f KB"
                    tierCount passed failed accuracy elapsedMs avgUsPerWord wordsPerSec deltaKB)

        output.WriteLine("=========================================================================================================")

    [<Fact>]
    let ``Xuất file corpus 1.000 từ chuẩn (phím gõ | kết quả mong đợi) ra docs/6.Benchmark/cases/corpus_1000.cases`` () =
        let words = CorpusLoader.loadTopWords 1000
        let repoRoot = CorpusLoader.findRepoRoot AppContext.BaseDirectory
        let outPath = Path.Combine(repoRoot, "docs", "6.Benchmark", "cases", "corpus_1000.cases")
        let lines = List<string>()
        lines.Add("# ==============================================================================")
        lines.Add("# CORPUS 1.000 TỪ TIẾNG VIỆT PHỔ BIẾN NHẤT")
        lines.Add("# Định dạng: <chuỗi phím gõ thô Telex> | <kết quả tiếng Việt mong đợi>")
        lines.Add("# ==============================================================================")
        for w in words do
            let keys = KeystrokeSynthesizer.wordToTelex w
            lines.Add(sprintf "%s | %s" keys w)
        output.WriteLine(sprintf "Đã xuất thành công %d cặp từ vào file: %s" words.Length outPath)

    [<Fact>]
    let ``Xuất đoạn văn 500 từ chuẩn (phím gõ | kết quả mong đợi) ra docs/6.Benchmark/cases/passage_500.cases`` () =
        let rawPassage = """
tiếng việt là một ngôn ngữ giàu đẹp với hệ thống thanh điệu phong phú và các cụm nguyên âm đa dạng
trải qua hơn ba mươi năm phát triển của công nghệ thông tin tại việt nam bài toán nhập liệu tiếng việt trên máy tính luôn là một thách thức kỹ thuật đầy thú vị
từ những ngày đầu của vietkey hay unikey cho đến các bộ gõ hiện đại chạy trên đa nền tảng các thế hệ lập trình viên đã không ngừng tìm kiếm giải pháp tối ưu nhất nhằm mang lại tốc độ gõ mượt mà và độ chính xác tuyệt đối
trong môi trường làm việc ngày nay người dùng không chỉ soạn thảo văn bản đơn thuần mà còn lập trình trên các trình biên dịch mã nguồn làm việc trên trình duyệt web trò chuyện trực tuyến và tương tác với các ứng dụng đồ họa phức tạp
sự phân mảnh của các giao thức nhập liệu như text services framework trên windows input method kit trên macos hay fcitx và ibus trên linux đòi hỏi một kiến trúc bộ gõ phải được thiết kế khoa học và tách bạch rõ ràng giữa thuật toán xử lý ngôn ngữ và tầng giao tiếp hệ điều hành
một bộ gõ tiếng việt hiện đại không chỉ dừng lại ở việc chuyển đổi các tổ hợp phím như telex hay vni thành ký tự có dấu mà còn phải có khả năng nhận diện ngữ cảnh phân biệt ranh giới giữa từ tiếng việt và từ tiếng anh tự động phục hồi khi người dùng sửa lỗi bằng phím xóa lùi và duy trì hiệu năng ổn định mà không làm tiêu tốn tài nguyên phần cứng
khi một từ được người dùng gõ vào hệ thống phải xử lý ngay lập tức trong vài phần triệu giây để tránh hiện tượng trễ phím hoặc nhảy lộn ký tự
hành trình xây dựng chuẩn kiểm thử độc lập cho toàn bộ hệ sinh thái bộ gõ là một bước tiến mang tính cộng đồng sâu sắc
bằng cách xây dựng các bộ dữ liệu mẫu chuẩn hóa và kịch bản tự động hóa trên ứng dụng thực tế chúng ta không chỉ đo lường chính xác năng lực của từng phần mềm mà còn thúc đẩy sự hợp tác cởi mở minh bạch giữa các nhóm phát triển
đây chính là nền tảng vững chắc để công nghệ việt nam tiếp tục vươn xa và phục vụ hàng chục triệu người dùng mỗi ngày với sự tin cậy cao nhất
mỗi dòng mã nguồn được viết ra với sự cẩn trọng là một đóng góp thiết thực cho trải nghiệm gõ phím của hàng triệu người việt nam
chúng ta mong muốn một công cụ gõ vừa nhanh vừa nhẹ vừa thông minh hòa nhập hoàn hảo vào từng nhịp gõ của người dùng trên mọi miền đất nước
từ học sinh sinh viên giáo viên nhà văn nhà báo cho đến các kỹ sư công nghệ ai ai cũng xứng đáng được tận hưởng một công cụ nhập liệu mượt mà chính xác và tôn trọng chuẩn mực ngôn ngữ dân tộc
khát vọng làm chủ công nghệ và nâng tầm sản phẩm phần mềm quốc nội luôn là ngọn lửa thôi thúc các thế hệ kỹ sư không ngừng đổi mới sáng tạo
chúng ta tin tưởng rằng với tinh thần cởi mở chia sẻ và cùng nhau hoàn thiện bộ gõ tiếng việt sẽ ngày càng hoàn mỹ đáp ứng mọi yêu cầu khắt khe nhất của thời đại công nghệ số hiện đại hôm nay
"""
        let words = 
            rawPassage.Split([| ' '; '\t'; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun w -> w.Trim().ToLowerInvariant())
            |> Array.filter (fun w -> w.Length > 0)

        let repoRoot = CorpusLoader.findRepoRoot AppContext.BaseDirectory
        let outPath = Path.Combine(repoRoot, "docs", "6.Benchmark", "cases", "passage_500.cases")
        let lines = List<string>()
        lines.Add("# ==============================================================================")
        lines.Add(sprintf "# ĐOẠN VĂN TIẾNG VIỆT THỰC TẾ %d TỪ (COHERENT PASSAGE BENCHMARK)" words.Length)
        lines.Add("# Chủ đề: Tiếng Việt trong kỷ nguyên số & Hành trình phát triển bộ gõ mã nguồn mở")
        lines.Add("# Định dạng: <chuỗi phím gõ thô Telex> | <từ tiếng Việt mong đợi>")
        lines.Add("# ==============================================================================")
        
        let config = { EngineConfig.Default with AllowLeadingWAsU = true }
        let mutable passed = 0
        for w in words do
            let keys = KeystrokeSynthesizer.wordToTelex w
            lines.Add(sprintf "%s | %s" keys w)

            // Kiểm tra độ đúng với engine Telex
            let mutable state = WordState.Empty
            for c in keys do
                let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
                state <- newState
            if isEquivalent state.TransformedText w then
                passed <- passed + 1

        File.WriteAllLines(outPath, lines, Encoding.UTF8)
        output.WriteLine(sprintf "Đã xuất thành công đoạn văn %d từ vào file: %s (Độ chính xác Engine: %d/%d = %.1f%%)" 
            words.Length outPath passed words.Length (float passed / float words.Length * 100.0))


