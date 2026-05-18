using LMKit.Agents.Resilience;
using System.Text;

namespace agent_resilience
{
    internal class Program
    {
        static int _attempt;

        static async Task Main()
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();
            PrintMenu();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "retry":
                        await DemoRetryAsync();
                        break;
                    case "2": case "breaker":
                        await DemoCircuitBreakerAsync();
                        break;
                    case "3": case "fallback":
                        await DemoFallbackAsync();
                        break;
                    case "4": case "composite":
                        await DemoCompositeAsync();
                        break;
                    case "5": case "all":
                        await DemoRetryAsync();
                        await DemoCircuitBreakerAsync();
                        await DemoFallbackAsync();
                        await DemoCompositeAsync();
                        break;
                    case "q": case "quit": case "exit":
                        return;
                    case "?": case "help": case "menu":
                        PrintMenu();
                        break;
                    default:
                        Console.WriteLine("Unknown choice. Type '?' to see the menu.");
                        break;
                }
            }
        }

        static Task<string> FlakyAsync(int failuresBeforeSuccess, string value)
        {
            _attempt++;
            if (_attempt <= failuresBeforeSuccess)
            {
                throw new InvalidOperationException($"Synthetic failure on attempt {_attempt}.");
            }
            return Task.FromResult(value);
        }

        static (int failures, string success) PromptFailureProfile(int defaultFailures, string defaultSuccess)
        {
            Console.Write($"Failures before success (default {defaultFailures}, '99' = always fail): ");
            if (!int.TryParse(Console.ReadLine(), out int f) || f < 0) { f = defaultFailures; }
            return (f, defaultSuccess);
        }

        static async Task DemoRetryAsync()
        {
            Console.WriteLine();
            Console.WriteLine("---- RetryPolicy (exponential backoff + jitter) ----");
            (int failures, string success) = PromptFailureProfile(defaultFailures: 2, defaultSuccess: "OK");
            _attempt = 0;
            RetryPolicy retry = new RetryPolicy(3)
                .WithExponentialBackoff(TimeSpan.FromMilliseconds(50), multiplier: 2.0, maxDelay: TimeSpan.FromSeconds(2))
                .WithJitter(0.2)
                .OnRetry((ex, attempt, delay) =>
                    Console.WriteLine($"  retry #{attempt} after {delay.TotalMilliseconds:F0} ms : {ex.Message}"));

            try
            {
                string result = await retry.ExecuteAsync(_ => FlakyAsync(failures, success), CancellationToken.None);
                Console.WriteLine($"  result: {result}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  exhausted: {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static async Task DemoCircuitBreakerAsync()
        {
            Console.WriteLine();
            Console.WriteLine("---- CircuitBreakerPolicy (open after 2 failures) ----");
            _attempt = 0;
            CircuitBreakerPolicy breaker = new CircuitBreakerPolicy(failureThreshold: 2, recoveryTime: TimeSpan.FromSeconds(1))
                .OnStateChange((from, to) => Console.WriteLine($"  state: {from} -> {to}"));
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    string r = await breaker.ExecuteAsync(_ => FlakyAsync(99, "never"), CancellationToken.None);
                    Console.WriteLine($"  result: {r}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  call {i + 1} -> {ex.GetType().Name}: {ex.Message}");
                }
            }
            Console.WriteLine();
        }

        static async Task DemoFallbackAsync()
        {
            Console.WriteLine();
            Console.WriteLine("---- FallbackPolicy<string> ----");
            _attempt = 0;
            FallbackPolicy<string> fallback = FallbackPolicy<string>.Create(
                fallbackValue: "(fallback content)",
                shouldHandle: ex => ex is InvalidOperationException);
            string result = await fallback.ExecuteAsync(_ => FlakyAsync(99, "never"), CancellationToken.None);
            Console.WriteLine($"  result: {result}");
            Console.WriteLine();
        }

        static async Task DemoCompositeAsync()
        {
            Console.WriteLine();
            Console.WriteLine("---- CompositePolicy (Retry x3 + Fallback) ----");
            _attempt = 0;
            CompositePolicy composite = new CompositePolicy()
                .Wrap(new RetryPolicy(3).WithDelay(TimeSpan.FromMilliseconds(10)))
                .Wrap(FallbackPolicy<string>.Create("(default reply)"));
            string result = await composite.ExecuteAsync<string>(_ => FlakyAsync(99, "never"), CancellationToken.None);
            Console.WriteLine($"  result: {result}");
            Console.WriteLine();
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Agent Resilience Policies Lab               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Retry / circuit-breaker / fallback / composite policies on a flaky operation.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / retry      RetryPolicy (exponential backoff + jitter)");
            Console.WriteLine("  2 / breaker    CircuitBreakerPolicy (opens after N failures)");
            Console.WriteLine("  3 / fallback   FallbackPolicy<T> (default value on failure)");
            Console.WriteLine("  4 / composite  CompositePolicy (Retry + Fallback)");
            Console.WriteLine("  5 / all        Run every demo back-to-back");
            Console.WriteLine("  q / quit       Exit");
            Console.WriteLine();
        }
    }
}
