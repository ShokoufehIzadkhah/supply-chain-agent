using Agent.Tools.Rag;

namespace Agent.Tools.Tests.Rag;

/// <summary>
/// Tests the Markdown chunker: the word budget, heading alignment and the split of oversized sections.
/// </summary>
public sealed class PolicyChunkerTests
{
    [Fact]
    public void Chunk_PacksSectionsUpToTheWordBudget()
    {
        // Four sections of 200 words each: 400 words fit in one chunk, the third section would exceed
        // the 500-word cap, so the document must come out as two full chunks of two sections each.
        var markdown = Document(
            Section(1, "One", 200),
            Section(2, "Two", 200),
            Section(3, "Three", 200),
            Section(4, "Four", 200));

        var chunks = PolicyChunker.Chunk(markdown, "delivery-terms.md");

        Assert.Equal(2, chunks.Count);

        foreach (var chunk in chunks)
        {
            Assert.InRange(WordCount(chunk.Text), PolicyChunker.MinWords, PolicyChunker.MaxWords);
            Assert.DoesNotContain('#', chunk.Text);
            Assert.Equal("delivery-terms.md", chunk.SourceFile);
        }

        Assert.Equal("1-2", chunks[0].Section);
        Assert.Equal("3-4", chunks[1].Section);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(1, chunks[1].ChunkIndex);
    }

    [Fact]
    public void Chunk_SplitsOnHeadingsAndKeepsTheHeadingPath()
    {
        var markdown = """
            # Returns Policy

            We accept business returns within 30 calendar days.

            ## 2. Return window

            The window is 30 calendar days from delivery.

            ### 2.1 Counting the days

            The window starts on the delivery date.
            """;

        var chunk = Assert.Single(PolicyChunker.Chunk(markdown, "returns-policy.md"));

        Assert.Equal("Returns Policy", chunk.Heading);
        Assert.Equal("2-2.1", chunk.Section);
        Assert.Contains("The window starts on the delivery date.", chunk.Text);
    }

    [Fact]
    public void Chunk_MovesWholeSectionsToAvoidATinyLastChunk()
    {
        var markdown = Document(
            Section(1, "one", 250),
            Section(2, "two", 250),
            Section(3, "three", 100));

        var chunks = PolicyChunker.Chunk(markdown, "priority-customers.md");

        Assert.Equal(2, chunks.Count);
        Assert.InRange(WordCount(chunks[1].Text), PolicyChunker.MinWords, PolicyChunker.MaxWords);

        // The whole of section 2 moved into the last chunk instead of leaving a 100-word tail behind.
        Assert.Contains("two", chunks[1].Text);
        Assert.Contains("three", chunks[1].Text);
        Assert.DoesNotContain("one", chunks[1].Text);
    }

    [Fact]
    public void Chunk_SplitsAnOversizedSectionOnParagraphBoundaries()
    {
        // A single 600-word section with no sub-heading still fits the budget, cut at blank lines.
        var markdown = Document(Section(1, "huge", 600));

        var chunks = PolicyChunker.Chunk(markdown, "minimum-order-quantities.md");

        Assert.Equal(2, chunks.Count);

        foreach (var chunk in chunks)
        {
            Assert.InRange(WordCount(chunk.Text), 1, PolicyChunker.MaxWords);
            Assert.Equal("1", chunk.Section);
        }
    }

    [Fact]
    public void Chunk_IgnoresHeadingsInsideCodeFences()
    {
        var markdown = """
            # Delivery Terms

            ## 1. Status codes

            The API is defined like this:

            ```
            # GET /orders/{id}
            ```

            That is all.
            """;

        var chunk = Assert.Single(PolicyChunker.Chunk(markdown, "delivery-terms.md"));

        Assert.Contains("# GET /orders/{id}", chunk.Text);
    }

    [Fact]
    public void Chunk_ReturnsNothingForAnEmptyDocument()
    {
        Assert.Empty(PolicyChunker.Chunk("# Title\n\n## 1. Empty\n", "empty.md"));
    }

    private static string Document(params string[] sections) => $"# Policy\n\n{string.Join("\n\n", sections)}";

    private static string Section(int number, string marker, int words)
        => $"## {number}. {marker}\n\n{string.Join(' ', Enumerable.Repeat(marker, words))}";

    private static int WordCount(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
