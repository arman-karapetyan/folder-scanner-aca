using System.Collections.Concurrent;

namespace folderscanner_aca;

class Program
{
    private static BlockingCollection<string> fileQueue = new BlockingCollection<string>();
    private static BlockingCollection<string> notifications = new BlockingCollection<string>();

    private static ConcurrentDictionary<string, WordStats> results = new ConcurrentDictionary<string, WordStats>();

    private static string[] searchWords;

    static void Main(string[] args)
    {
        Console.Write("Enter directory path(empty = current direction): ");
        string rootPath = Console.ReadLine();
        if (string.IsNullOrEmpty(rootPath))
        {
            rootPath = Environment.CurrentDirectory;
        }

        Console.Write("Enter file extension(empty = *.txt): ");
        string extension = Console.ReadLine();
        if (string.IsNullOrEmpty(extension))
        {
            extension = "*.txt";
        }

        Console.Write("Enter search words(comma separated): ");
        searchWords = Console
            .ReadLine()
            .Split(',')
            .Select(w => w.Trim().ToLower())
            .ToArray();

        Thread printerThread = new Thread(PrintNotifications);
        printerThread.Start();

        Thread scannerThread = new Thread(() =>
        {
            ScanFolders(rootPath, extension);
            fileQueue.CompleteAdding();
        });
        scannerThread.Start();

        Thread[] workers = new Thread[4];
        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = new Thread(ProcessFiles);
            workers[i].Start();
        }

        foreach (Thread worker in workers)
        {
            worker.Join();
        }

        notifications.CompleteAdding();
        printerThread.Join();

        PrintSummary();
    }

    private static void PrintNotifications()
    {
        foreach (var msg in notifications.GetConsumingEnumerable())
        {
            Console.WriteLine(msg);
        }
    }

    private static void PrintSummary()
    {
        Console.WriteLine("\n===== SUMMARY ======");
        foreach (var pair in results)
        {
            Console.WriteLine($"Word: {pair.Key}");
            Console.WriteLine($"\tTotal: {pair.Value.TotalCount}");
            Console.WriteLine($"\t Files:");
            foreach (string file in pair.Value.Files)
            {
                Console.WriteLine($"\t\t-{file}");
            }
        }
    }

    private static void ProcessFiles()
    {
        foreach (var file in fileQueue.GetConsumingEnumerable())
        {
            var content = File.ReadAllText(file).ToLower();
            foreach (var word in searchWords)
            {
                int count = CountOccurrences(content, word);
                if (count > 0)
                {
                    var stats = results.GetOrAdd(word, _ => new WordStats());
                    lock (stats)
                    {
                        stats.TotalCount += count;
                        stats.Files.Add(file);
                    }

                    notifications.Add($"[Found] '{word}' in {Path.GetFileName(file)} ({count} times)");
                    Thread.Sleep(Random.Shared.Next(100, 200));
                }
            }

            Thread.Sleep(Random.Shared.Next(500, 800));
        }
    }

    private static int CountOccurrences(string text, string word)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(word, index)) != -1)
        {
            count++;
            index += word.Length;
        }

        return count;
    }

    private static void ScanFolders(string path, string extension)
    {
        foreach (var file in Directory.GetFiles(path, extension))
        {
            fileQueue.Add(file);
        }

        foreach (var dir in Directory.GetDirectories(path))
        {
            ScanFolders(dir, extension);
        }
    }
}

public class WordStats
{
    public int TotalCount { get; set; }
    public HashSet<string> Files = new HashSet<string>();
}