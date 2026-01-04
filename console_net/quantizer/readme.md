# Model Quantizer

A demo for quantizing GGUF models to smaller sizes using LM-Kit.NET. Reduce model file size and VRAM requirements while balancing quality.

## Features

- Quantize GGUF models to various precision formats
- Support for all common quantization levels (Q2 through Q8)
- Batch quantization to all formats at once
- Automatic output file naming

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Source model in GGUF format (FP16 recommended)

## Usage

1. Run the application
2. Enter the path to your source model (FP16 GGUF recommended)
3. Select a quantization format
4. The quantized model is saved alongside the original

## Quantization Formats

| Format | Size | Quality | Recommendation |
|--------|------|---------|----------------|
| Q2_K | Smallest | Significant loss | Not recommended |
| Q3_K_S | Very small | High loss | |
| Q3_K_M | Very small | High loss | |
| Q3_K_L | Small | Substantial loss | |
| Q4_K_S | Small | Greater loss | |
| Q4_K_M | Medium | Balanced | ✓ Recommended |
| Q5_K_S | Large | Low loss | ✓ Recommended |
| Q5_K_M | Large | Very low loss | ✓ Recommended |
| Q6_K | Very large | Extremely low loss | |
| Q8_0 | Very large | Extremely low loss | |

## Batch Quantization

Select `ALL` to generate versions in all quantization formats at once.