# Chart Data Harvester

Finds every chart in a folder of PDFs, scans and images, reads the data each one plots with
LM-Kit.NET's AI document parser, and writes it as CSV beside the chart's picture, fully
on-device.

## Features

- Every chart of every page found and read as a data table (`ChartContent`).
- The title the chart prints, its legend, and one table per panel for multi-panel figures.
- One CSV per chart (and per panel), the chart's picture as PNG, and an `index.csv`.
- A review page per document (`SaveHtml`): each chart's picture beside its data.

## Prerequisites

- .NET 8.0 or later
- A GPU is recommended (the demo parses at `High` effort)
- About 2.5 GB of disk for the parser's models, downloaded on first run

## How It Works

1. `new DocumentParser(ParsingEffort.High) { IncludeFigureImages = true }`: the most faithful
   reading, with the pictures kept.
2. Each document is parsed; every `ChartContent` element is exported.
3. `ChartContent.Text` (a Markdown table) converts to CSV; `ParsedElement.GetImage()` gives the picture.

## Usage

```bash
dotnet run
```

Enter a folder. The results land in `<folder>/charts/`.

## Example Output

```
paper.pdf
  page 6: Figure 5. A tree with two traces, a trunk trace and one branch trace. The t... (1 table)
  page 11: Figure 10. Speedup vs. a baseline JavaScript interpreter (SpiderMonkey) for... (1 table)

8 chart(s) harvested into reports\charts
```

`paper-p11-c1.csv`:

```
Characteristic,Tracing,SFX,V8
3d-cube,2.2,2.0,3.0
3d-morph,3.0,2.0,2.3
3d-raytrace,1.3,2.8,2.6
```

`index.csv`:

```
document,page,chart,title,csv,picture
paper.pdf,11,1,"Figure 10. Speedup vs. a baseline JavaScript interpreter ...",paper-p11-c1.csv,paper-p11-c1.png
```

## Configuration

| Setting | Where | Effect |
|---|---|---|
| Effort level | `Effort` constant | `High` by default; `Medium` is faster |

## Learn More

- [Extract Chart Data from Documents](https://docs.lm-kit.com/lm-kit-net/guides/how-to/extract-chart-data-from-documents.html)
