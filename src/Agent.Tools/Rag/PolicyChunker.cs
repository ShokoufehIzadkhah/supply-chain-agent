using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Tools.Rag;

/// <summary>
/// Splits a Markdown policy document into chunks that a vector search can retrieve.
/// <para>
/// The splitter works on Markdown headings rather than on a fixed character count, so a chunk never
/// begins in the middle of a paragraph and each chunk knows the heading it belongs to. Sections are
/// packed greedily up to <see cref="MaxWords"/> words; a section that is too large is cut at blank
/// lines (paragraph boundaries) first and only sliced mid-paragraph as a last resort. A tail chunk
/// below the 300-word target is rebalanced with whole sections taken from the chunk before it, so the
/// 300–500 word target holds wherever the document structure allows it.
/// </para>
/// </summary>
public static partial class PolicyChunker
{
    /// <summary>Target lower bound for a chunk, in words. Chunks are grown towards it where possible.</summary>
    public const int MinWords = 300;

    /// <summary>Hard upper bound for a chunk, in words.</summary>
    public const int MaxWords = 500;

    private const string HeadingSeparator = " > ";

    /// <summary>Splits one Markdown document into chunks.</summary>
    /// <param name="markdown">Full Markdown text of the document.</param>
    /// <param name="sourceFile">File name stored on every chunk, for example <c>returns-policy.md</c>.</param>
    /// <returns>The chunks in document order. An empty list when the document has no body text.</returns>
    public static IReadOnlyList<PolicyChunk> Chunk(string markdown, string sourceFile)
    {
        var groups = new List<List<Piece>>();
        var current = new List<Piece>();
        var currentWords = 0;

        foreach (var piece in ParseSections(markdown, sourceFile).SelectMany(SplitOversized))
        {
            // Packing is greedy: a section joins the current chunk unless it would push it over the cap.
            if (current.Count > 0 && currentWords + piece.WordCount > MaxWords)
            {
                groups.Add(current);
                current = [];
                currentWords = 0;
            }

            current.Add(piece);
            currentWords += piece.WordCount;
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        Rebalance(groups);

        return [.. groups.Select((group, index) => BuildChunk(group, sourceFile, index))];
    }

    /// <summary>
    /// Greedy packing can leave a very small last chunk, which is poor retrieval material. Move whole
    /// sections from the previous chunk into the last one while that keeps both sides sensible.
    /// </summary>
    private static void Rebalance(List<List<Piece>> groups)
    {
        if (groups.Count < 2)
        {
            return;
        }

        var tail = groups[^1];
        var previous = groups[^2];
        var tailWords = tail.Sum(piece => piece.WordCount);

        while (tailWords < MinWords && previous.Count > 1)
        {
            var candidate = previous[^1];

            // Stop when the section is too large to move, or would push the tail over the cap.
            if (tailWords + candidate.WordCount > MaxWords)
            {
                break;
            }

            previous.RemoveAt(previous.Count - 1);
            tail.Insert(0, candidate);
            tailWords += candidate.WordCount;
        }
    }

    private static PolicyChunk BuildChunk(List<Piece> group, string sourceFile, int index)
    {
        var numbers = group
            .Select(piece => piece.Number)
            .Where(number => number is not null)
            .Distinct()
            .ToList();

        var section = numbers.Count switch
        {
            0 => null,
            1 => numbers[0],
            _ => $"{numbers[0]}-{numbers[^1]}",
        };

        return new PolicyChunk(
            sourceFile,
            group[0].Heading,
            section,
            string.Join("\n\n", group.Select(piece => piece.Text)),
            index);
    }

    /// <summary>Reads the document into one piece per heading, carrying the full heading path.</summary>
    private static List<Piece> ParseSections(string markdown, string sourceFile)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var documentTitle = Path.GetFileNameWithoutExtension(sourceFile);
        var stack = new List<Heading>();
        var pieces = new List<Piece>();
        var body = new StringBuilder();
        var number = (string?)null;
        var inCodeFence = false;

        void Flush()
        {
            var text = body.ToString().Trim();
            body.Clear();

            if (text.Length == 0)
            {
                return;
            }

            var heading = stack.Count == 0
                ? documentTitle
                : string.Join(HeadingSeparator, stack.Select(entry => entry.Text));

            pieces.Add(new Piece(heading, number, text, CountWords(text)));
        }

        foreach (var line in lines)
        {
            // Headings inside a fenced code block are content, not structure.
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal) ||
                line.TrimStart().StartsWith("~~~", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                body.AppendLine(line);
                continue;
            }

            var heading = inCodeFence ? null : HeadingPattern().Match(line);

            if (heading is not { Success: true })
            {
                body.AppendLine(line);
                continue;
            }

            Flush();

            var level = heading.Groups["hashes"].Value.Length;
            var text = heading.Groups["text"].Value.Trim();

            while (stack.Count > 0 && stack[^1].Level >= level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            stack.Add(new Heading(level, text));
            number = LeadingSectionNumber(text);
        }

        Flush();

        return pieces;
    }

    /// <summary>Cuts a section that is larger than a chunk, preferring paragraph boundaries.</summary>
    private static IEnumerable<Piece> SplitOversized(Piece piece)
    {
        if (piece.WordCount <= MaxWords)
        {
            yield return piece;
            yield break;
        }

        var paragraphs = piece.Text.Split(
            "\n\n",
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var buffer = new StringBuilder();
        var bufferedWords = 0;

        foreach (var paragraph in paragraphs)
        {
            var words = CountWords(paragraph);

            if (words > MaxWords)
            {
                // A single oversized paragraph, for example one huge table row: slice it by words.
                if (bufferedWords > 0)
                {
                    yield return piece with { Text = buffer.ToString(), WordCount = bufferedWords };
                    buffer.Clear();
                    bufferedWords = 0;
                }

                foreach (var slice in SliceByWords(paragraph))
                {
                    yield return piece with { Text = slice, WordCount = CountWords(slice) };
                }

                continue;
            }

            if (bufferedWords > 0 && bufferedWords + words > MaxWords)
            {
                yield return piece with { Text = buffer.ToString(), WordCount = bufferedWords };
                buffer.Clear();
                bufferedWords = 0;
            }

            if (buffer.Length > 0)
            {
                buffer.Append("\n\n");
            }

            buffer.Append(paragraph);
            bufferedWords += words;
        }

        if (bufferedWords > 0)
        {
            yield return piece with { Text = buffer.ToString(), WordCount = bufferedWords };
        }
    }

    private static IEnumerable<string> SliceByWords(string paragraph)
    {
        var words = SplitWords(paragraph);

        for (var start = 0; start < words.Length; start += MaxWords)
        {
            yield return string.Join(' ', words[start..Math.Min(start + MaxWords, words.Length)]);
        }
    }

    /// <summary>Reads the leading section number of a heading, for example <c>3</c> from "3. The five-day rule".</summary>
    private static string? LeadingSectionNumber(string heading)
    {
        var match = SectionNumberPattern().Match(heading);
        return match.Success ? match.Groups["number"].Value.TrimEnd('.') : null;
    }

    private static string[] SplitWords(string text)
        => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static int CountWords(string text) => SplitWords(text).Length;

    [GeneratedRegex(@"^(?<hashes>#{1,6})[ \t]+(?<text>.+?)[ \t]*$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(?<number>\d+(?:\.\d+)*)\.?(?:\s|$)")]
    private static partial Regex SectionNumberPattern();

    private sealed record Heading(int Level, string Text);

    private readonly record struct Piece(string Heading, string? Number, string Text, int WordCount);
}
