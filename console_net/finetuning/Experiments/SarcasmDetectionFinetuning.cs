/*
    This experiment illustrates the process of fine-tuning a tiny LLaMA model 
    to significantly enhance the accuracy of LMKit's sarcasm detection engine.
    During the training process, the console should display an accuracy improvement 
    from approximately 50% to 85+%.
*/

using System.Diagnostics;
using LMKit.Finetuning;
using LMKit.Model;
using LMKit.TextAnalysis;
using static LMKit.TextAnalysis.SarcasmDetection;

namespace finetuning.Experiments
{
    internal static class SarcasmDetectionFinetuning
    {
        private static readonly string DefaultModelPath = @"https://huggingface.co/lm-kit/llama-3.2-1b-instruct.gguf/resolve/main/Llama-3.2-1B-Instruct-F16.gguf?download=true";

        // Early-stop conditions
        private const float StopTrainingAtLoss = 0.01f;
        private static readonly TimeSpan MaxTrainingDuration = TimeSpan.FromDays(3);

        private const int EvaluateLoraAdapterEveryIterationCount = 10;
        private const int SaveTrainingCheckpointEveryIterationCount = 10;

        private const string BestLoraPath = "sarcasmDetection.lora.best_accuracy.bin";
        private const string BestCheckpointPath = "sarcasmDetection_best_checkpoint.bin";
        private const string BestModelPath = "sarcasmDetection.best_accuracy.gguf";
        private const string EvalModelPath = "sarcasmDetection.test.gguf";
        private static readonly float[] LoraTestScales = { 0.75f, 1f, 1.25f, 1.6f };

        private static LM _model;
        private static double _bestLoss;
        private static double _bestAccuracy;
        private static double _initialAccuracy;
        private static float _loraBestScale;
        //dataset used to evaluate the LoRA adapter accuracy.
        private static readonly List<(string, bool)> BlindTestDataset = GetTrainingData(TrainingDataset.OpenAIEvalsSarcasm,
                                                                                        maxSamples: 300,
                                                                                        shuffle: true,
                                                                                        seed: 7263);

        public static void RunTraining()
        {
            _model = ModelUtils.LoadModel(DefaultModelPath);
            _bestLoss = 100;
            _bestAccuracy = 0;

            Console.WriteLine("Computing initial model accuracy...");
            _bestAccuracy = _initialAccuracy = ComputeSarcasmDetectionAccuracy("", -1, out TimeSpan elapsed);
            double speed = BlindTestDataset.Count / elapsed.TotalSeconds;
            Console.WriteLine($"The initial model accuracy is {Math.Round(_bestAccuracy, 2):F2}% - {Math.Round(speed, 2)} samples/s.");

            var engine = new SarcasmDetection(_model);

            var finetuning = engine.CreateTrainingObject(TrainingDataset.OpenAIEvalsSarcasm,
                                                         maxSamples: 2000,
                                                         shuffle: true,
                                                         seed: 923);

            finetuning.Iterations = 20000;
            finetuning.ContextSize = 128;

            finetuning.TrainingCheckpoint = ""; //can be used to resume a previous training session.

            finetuning.LoraTrainingParameters.AdamAlpha = 0.0002f;
            finetuning.BatchSize = 8;

            if (SystemUtils.GetTotalMemoryGB() >= 30 &&
                _model.ParameterCount < 2000000000)
            {
                finetuning.UseGradientCheckpointing = false; // switch back to true if the training process consumes all the memory.
            }

            _ = finetuning.FilterSamplesBySize(0, finetuning.ContextSize);

            finetuning.FinetuningProgress += FinetuningProgress;

            // Finetuning to a LoRA adapter
            finetuning.Finetune2Lora("sarcasmDetection.lora.last.bin");

            Console.WriteLine("\nProcess terminated. Press any key to exit");
            _ = Console.ReadKey();
        }

        private static double ComputeSarcasmDetectionAccuracy(string loraPath, float loraScale, out TimeSpan elapsed)
        {
            LM model = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(loraPath))
                {
                    var merger = new LoraMerger(_model);
                    merger.AddLoraAdapter(new LoraAdapterSource(loraPath, loraScale));
                    merger.Merge(EvalModelPath);
                    model = new LM(EvalModelPath);
                }
                else
                {
                    model = new LM(DefaultModelPath);
                }

                var engine = new SarcasmDetection(model);
                Stopwatch stopwatch = Stopwatch.StartNew();
                int successCount = 0;

                foreach (var sample in BlindTestDataset)
                {
                    var hasSarcasm = engine.IsSarcastic(sample.Item1);

                    if (hasSarcasm == sample.Item2)
                    {
                        successCount++;
                    }
                }

                stopwatch.Stop();
                elapsed = stopwatch.Elapsed;

                return (double)successCount / BlindTestDataset.Count * 100;
            }
            finally
            {
                model?.Dispose();
            }
        }

        private static bool EvaluateLoraAccuracy(string loraPath, float loraScale)
        {
            Console.WriteLine($"Evaluating LoRA adapter accuracy with scale {loraScale}...");

            double accuracy = ComputeSarcasmDetectionAccuracy(loraPath, loraScale, out TimeSpan elapsed);
            double speed = BlindTestDataset.Count / elapsed.TotalSeconds;

            if (accuracy > _bestAccuracy)
            {
                _bestAccuracy = accuracy;
                _loraBestScale = loraScale;
                File.Copy(loraPath, BestLoraPath, overwrite: true);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"LoRA adapter best accuracy is now: {Math.Round(accuracy, 2)}% - LoRA scale: {loraScale} - {Math.Round(speed, 2)} samples/s.");
                Console.ResetColor();
                Console.Beep();

                return true;
            }
            else
            {
                Console.WriteLine($"LoRA adapter accuracy: {Math.Round(accuracy, 2)}% with scale {loraScale} - Best: {Math.Round(_bestAccuracy, 2)}% with scale {_loraBestScale} - Initial: {Math.Round(_initialAccuracy, 2)}% - {Math.Round(speed, 2)} samples/s.");
                return false;
            }
        }

        private static void FinetuningProgress(object sender, FinetuningProgressEventArgs e)
        {
            Console.WriteLine($"Progress: {Math.Round(e.Percentage, 2)}%. Epochs: {e.Epochs}. Iter.: {e.Iterations}/{e.IterationCount}. Next Sample: {e.NextSample}/{e.SampleCount}. Loss: {Math.Round(e.Loss, 2)}. Elapsed: {e.Elapsed:dd\\.hh\\:mm\\:ss}. Rem.: {e.Remaining?.ToString(@"dd\.hh\:mm\:ss") ?? "#"}");

            if (e.Iterations > 1 && e.Loss <= StopTrainingAtLoss)
            {
                e.Stop = true;
                Console.WriteLine("Stopping fine-tuning as the minimum loss has been achieved.");
            }
            else if (e.Elapsed > MaxTrainingDuration)
            {
                e.Stop = true;
                Console.WriteLine("Stopping finetuning because maximum training duration has been reached.");
            }

            if (e.Iterations > 0)
            {
                string loraPath = "";

                if (e.BestLoss < _bestLoss)
                {
                    _bestLoss = e.BestLoss;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Best training loss is now: {Math.Round(_bestLoss, 2)}");
                    Console.ResetColor();

                    if (e.Loss < 2) //We set a maximum loss limit of 2 to avoid performing unnecessary accuracy computations at the beginning of the training process.
                    {
                        loraPath = "sarcasmDetection.lora.best_loss.bin";
                        e.SaveLora(loraPath);
                    }
                }

                if (e.Iterations % EvaluateLoraAdapterEveryIterationCount == 0 || e.Percentage == 100)
                {
                    loraPath = "sarcasmDetection.lora.last.bin";
                    e.SaveLora(loraPath);
                }

                if (loraPath != "")
                {
                    foreach (float scale in LoraTestScales)
                    {
                        if (EvaluateLoraAccuracy(loraPath, scale))
                        {
                            e.SaveLoraCheckpoint(BestCheckpointPath);
                            File.Copy(EvalModelPath, BestModelPath, overwrite: true);
                            Console.WriteLine($"Best model created at {Path.GetFullPath(BestModelPath)}");
                        }
                    }
                }

                if (e.Iterations % SaveTrainingCheckpointEveryIterationCount == 0)
                {
                    string dstPath = $"training.checkpoint.last.bin";
                    e.SaveLoraCheckpoint(dstPath);
                }
            }
        }
    }
}