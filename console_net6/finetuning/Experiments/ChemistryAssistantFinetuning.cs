/*
    This experiment demonstrates the process of fine-tuning a small LLaMA model to function as a chemistry assistant. 
    The resulting model can subsequently be inferred by the SingleTurnConversation API provided by LMKit.

    Minimum Required System RAM: 16 GB.

    :: Initial accuracy: 16.67%.
    :: Obtained accuracy after fine-tuning:
         - Windows: 25.93%. At iteration 3651.
         - macOS: 38.89%. At iteration 2570.
*/

using LMKit.Model;
using LMKit.Finetuning;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Diagnostics;

namespace finetuning.Experiments
{
    internal static class ChemistryAssistantFinetuning
    {
        private static readonly string DefaultModelPath = @"https://huggingface.co/TheBloke/TinyLlama-1.1B-1T-OpenOrca-GGUF/resolve/main/tinyllama-1.1b-1t-openorca.Q8_0.gguf";
        private const string DatasetURI = "https://raw.githubusercontent.com/YuanTony/chemistry-assistant/main/fine-tune-model/train.txt";
        // Early-stop conditions
        private const float StopTrainingAtLoss = 0.01f;
        private static readonly TimeSpan MaxTrainingDuration = TimeSpan.FromDays(3);

        private const int EvaluateLoraAdapterEveryIterationCount = 10;
        private const int SaveTrainingCheckpointEveryIterationCount = 10;

        private const string BestLoraPath = "chemistryAssistant.lora.best_accuracy.bin";
        private const string BestCheckpointPath = "chemistryAssistant_best_checkpoint.bin";
        private const string NewModelPath = "chemistryAssistant.gguf";
        private static readonly float[] LoraTestScales = { 0.75f, 1f, 1.25f, 1.6f };

        private static LLM _model;
        private static string _trainingDataset;
        // Dataset used to evaluate the LoRA adapter accuracy.
        private static List<(string, int)> _testingDataset;
        private static double _bestLoss;
        private static double _bestAccuracy;
        private static float _loraBestScale;
        private static double _initialAccuracy;

        public static void RunTraining()
        {
            _trainingDataset = DownloadDataset();
            _testingDataset = CreateTestingDataset();

            _model = ModelUtils.LoadModel(DefaultModelPath);
            _bestLoss = 100;
            _bestAccuracy = 0;

            _bestAccuracy = _initialAccuracy = ComputeAccuracy("", 1, out TimeSpan elapsed, out _);
            double speed = _testingDataset.Count / elapsed.TotalSeconds;
            Console.WriteLine($"The initial model accuracy is {Math.Round(_bestAccuracy, 2):F2}% - {Math.Round(speed, 2)} samples/s.");

            var finetuning = new LoraFinetuning(_model);

            _ = finetuning.LoadTrainingDataFromText(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_trainingDataset)));

            finetuning.BatchSize = 8;
            finetuning.Iterations = 4000;
            finetuning.ContextSize = 192;

            finetuning.TrainingCheckpoint = ""; // Can be used to resume a previous training session.

            finetuning.LoraTrainingParameters.LoraAlpha = 16;
            finetuning.LoraTrainingParameters.LoraRank = 16;
            finetuning.LoraTrainingParameters.AdamAlpha = 0.0001f;

            _ = finetuning.FilterSamplesBySize(0, finetuning.ContextSize);

            if (SystemUtils.GetTotalMemoryGB() >= 30 &&
                _model.ParameterCount < 2000000000)
            {
                finetuning.UseGradientCheckpointing = false; // Switch back to true if the training process consumes all the memory.
            }

            finetuning.FinetuningProgress += FinetuningProgress;

            // Finetuning to a LoRA adapter
            finetuning.Finetune2Lora("chemistryAssistant.lora.last.bin");

            // Creating a model
            Console.WriteLine("Creating a model...");
            var merger = new LoraMerger(_model);
            merger.AddLoraAdapter(new LoraAdapterSource(BestLoraPath, scale: _loraBestScale)); // Using the adapter with the best accuracy.
            merger.Merge(NewModelPath);
            Console.WriteLine($"Model created at {Path.GetFullPath(NewModelPath)}");
            Console.WriteLine("\nProcess terminated. Press any key to exit");
            _ = Console.ReadKey();
        }

        private static double ComputeAccuracy(string loraPath, float loraScale, out TimeSpan elapsed, out bool isBest)
        {
            isBest = false;

            bool initialAccuracyMode = string.IsNullOrWhiteSpace(loraPath);

            if (!initialAccuracyMode)
            {
                Console.WriteLine($"Evaluating LoRA adapter accuracy with scale {loraScale}...");
            }
            else
            {
                Console.WriteLine("Computing initial model accuracy...");
            }

            using var model = new LLM(DefaultModelPath);

            if (!initialAccuracyMode)
            {
                model.ApplyLoraAdapter(new LoraAdapterSource(loraPath, loraScale));
            }

            var chat = new SingleTurnConversation(model)
            {
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = "You are an expert chemistry assistant. Provide precise and accurate answers to questions related to chemistry."
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            int successCount = 0;

            foreach (var sample in _testingDataset)
            {
                var answer = chat.Submit(sample.Item1);

                if (int.TryParse(PopNumber(answer.Completion), out int result))
                {
                    if (result == sample.Item2)
                    {
                        successCount++;
                    }
                }
            }

            stopwatch.Stop();
            elapsed = stopwatch.Elapsed;

            double accuracy = (double)successCount / _testingDataset.Count * 100;

            if (initialAccuracyMode)
            {
                _bestAccuracy = accuracy;
            }
            else
            {
                double speed = _testingDataset.Count / elapsed.TotalSeconds;

                if (accuracy > _bestAccuracy)
                {
                    _bestAccuracy = accuracy;
                    _loraBestScale = loraScale;
                    File.Copy(loraPath, BestLoraPath, overwrite: true);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"LoRA adapter best accuracy is now: {Math.Round(accuracy, 2)}% - LoRA scale: {loraScale} - {Math.Round(speed, 2)} samples/s.");
                    Console.ResetColor();
                    Console.Beep();
                    isBest = true;
                }
                else
                {
                    Console.WriteLine($"LoRA adapter accuracy: {Math.Round(accuracy, 2)}% with scale {loraScale} - Best: {Math.Round(_bestAccuracy, 2)}% with scale {_loraBestScale} - Initial: {Math.Round(_initialAccuracy, 2)}% - {Math.Round(speed, 2)} samples/s.");
                }
            }

            return accuracy;
        }

        private static string DownloadDataset()
        {
            using var client = new HttpClient();
            return client.GetStringAsync(DatasetURI).Result;
        }

        private static List<(string, int)> CreateTestingDataset()
        {
            List<(string, int)> testingDataset = new();
            string[] samples = _trainingDataset.Split("<SFT>", StringSplitOptions.RemoveEmptyEntries);

            foreach (string sample in samples)
            {
                if (sample.Contains("What is the atomic number of"))
                {
                    int start = sample.IndexOf("What is the atomic number of");
                    int end = sample.IndexOf(" [/INST]");
                    string question = sample.Substring(start, end - start);
                    string answer = PopNumber(sample);

                    testingDataset.Add((question, int.Parse(answer)));
                }
            }

            return testingDataset;
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

                    if (e.Loss < 2) // We set a maximum loss limit of 2 to avoid performing unnecessary accuracy computations at the beginning of the training process.
                    {
                        loraPath = "chemistryAssistant.lora.best_loss.bin";
                        e.SaveLora(loraPath);
                    }
                }

                if (e.Iterations % EvaluateLoraAdapterEveryIterationCount == 0 || e.Percentage == 100)
                {
                    loraPath = "chemistryAssistant.lora.last.bin";
                    e.SaveLora(loraPath);
                }

                if (loraPath != "")
                {
                    foreach (float scale in LoraTestScales)
                    {
                        _ = ComputeAccuracy(loraPath, scale, out _, out bool isBest);

                        if (isBest)
                        {
                            e.SaveLoraCheckpoint(BestCheckpointPath);
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

        private static string PopNumber(string value)
        {
            string numChars = "";

            foreach (char c in value)
            {
                if (char.IsDigit(c))
                {
                    numChars += c;
                }
                else if (numChars.Length > 0)
                {
                    break;
                }
            }

            return numChars;
        }
    }
}
