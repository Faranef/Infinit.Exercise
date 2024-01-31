using Octokit;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Infinit.Exercise;

internal class Program
{
    static async Task Main(string[] args)
    {
        var username = "userName";
        var token = "github Token";

        var client = new GitHubClient(new ProductHeaderValue("lodash"));
        var basicAuth = new Credentials(username, token);
        client.Credentials = basicAuth;

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            var allContent = await GetAllContentsRecursively(client, "lodash", "lodash");

            var jsFiles = allContent.Where(content => content.Type == ContentType.File && content.Name.EndsWith(".js")).ToList();
            var tsFiles = allContent.Where(content => content.Type == ContentType.File && content.Name.EndsWith(".ts")).ToList();

            ConcurrentDictionary<char, int> jsLetterCounts = [];
            ConcurrentDictionary<char, int> tsLetterCounts = [];

            Parallel.ForEach(jsFiles, file =>
            {
                PrepareForCounting(file, client, jsLetterCounts);
            });

            Parallel.ForEach(tsFiles, file =>
            {
                PrepareForCounting(file, client, tsLetterCounts);
            });

            var combinedLetterCounts = CombineLetterCounts(jsLetterCounts, tsLetterCounts);

            foreach (var entry in combinedLetterCounts.OrderBy(e => e.Key))
            {
                Console.WriteLine($"{entry.Key}: {entry.Value} occurrences");
            }

            sw.Stop();
            Console.WriteLine($"Elapsed time: {sw.Elapsed}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message} \n StackTrace: {ex.StackTrace}");
        }
    }

    private static async Task<ConcurrentDictionary<char, int>>? PrepareForCountingAsync(List <RepositoryContent> jsFiles, GitHubClient client)
    {
        ConcurrentDictionary<char, int> letters = [];
        foreach (var file in jsFiles)
        {
            var fileContent = await client.Repository.Content.GetRawContent("lodash", "lodash", file.Path);
            var contentString = Encoding.UTF8.GetString(fileContent);
            CountLetters(contentString, letters);
        }

        return letters;
    }

    private static void PrepareForCounting(RepositoryContent file, GitHubClient client, ConcurrentDictionary<char, int> jsLetterCounts)
    {
        var fileContent = client.Repository.Content.GetRawContent("lodash", "lodash", file.Path).Result;
        var contentString = Encoding.UTF8.GetString(fileContent);
        CountLetters(contentString, jsLetterCounts);
    }

    static Dictionary<char, int> CombineLetterCounts(ConcurrentDictionary<char, int> dict1, ConcurrentDictionary<char, int> dict2)
    {
        var combinedDict = new Dictionary<char, int>();

        foreach (var entry in dict1.Concat(dict2))
        {
            lock (combinedDict)
            {
                if (combinedDict.ContainsKey(entry.Key))
                {
                    combinedDict[entry.Key] += entry.Value;
                }
                else
                {
                    combinedDict[entry.Key] = entry.Value;
                }
            }
        }

        return combinedDict;
    }

    static async Task<List<RepositoryContent>> GetAllContentsRecursively(GitHubClient client, string owner, string repo, string path = ".")
    {
        List<RepositoryContent> allContent = [];

        var contents = await client.Repository.Content.GetAllContents(owner, repo, path);

        foreach (var content in contents)
        {
            if (content.Type == ContentType.Dir)
            {
                var subdirectoryContents = await GetAllContentsRecursively(client, owner, repo, content.Path);
                allContent.AddRange(subdirectoryContents);
            }
            else
            {
                if (content.Type == ContentType.File && content.Name.EndsWith(".js") ||
                    content.Type == ContentType.File && content.Name.EndsWith(".ts"))
                {
                    allContent.Add(content);
                }
            }
        }

        return allContent;
    }

    static void CountLetters(string content, ConcurrentDictionary<char, int> letterCounts)
    {
        foreach (char character in content)
        {
            if (char.IsLetter(character))
            {
                lock (letterCounts)
                {
                    char lowercaseChar = char.ToLower(character);
                    letterCounts.AddOrUpdate(lowercaseChar, 1, (k, v) => v + 1);
                }
            }
        }
    }
}
