using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

class Program
{
  static void PrintUsage()
  {
    Console.WriteLine("Usage: dotnet run");
    Console.WriteLine();
    Console.WriteLine("Downloads every image attachment in the ARTWORK_IMAGE and ARCHIVE_IMAGE");
    Console.WriteLine("tables to local disk. Each attachment becomes <prefix>_<recordId>_<size>.<ext>");
    Console.WriteLine("under <OutputDirectory>/artwork/ and <OutputDirectory>/archive/.");
    Console.WriteLine("Already-downloaded files are skipped (safe to re-run).");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -h, --help, -?, /?, ?            show this help and exit");
    Console.WriteLine();
    Console.WriteLine("Configuration (appsettings.json):");
    Console.WriteLine("  Airtable:ApiKey                  personal access token");
    Console.WriteLine("  Airtable:BaseId                  base to read");
    Console.WriteLine("  ImageDownload:OutputDirectory    download root (default: images)");
  }

  static async Task<int> Main(string[] args)
  {
    if (args.Any(a => a is "-h" or "--help" or "-?" or "/?" or "?"))
    {
      PrintUsage();
      return 0;
    }
    foreach (var a in args)
    {
      if (a.StartsWith("-") || a.StartsWith("/"))
      {
        Console.WriteLine($"Unknown option: {a}");
        Console.WriteLine();
        PrintUsage();
        return 1;
      }
    }

    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory())
      .AddJsonFile("appsettings.json")
      .Build();

    var airtableApiKey = configuration["Airtable:ApiKey"];
    var airtableBaseId = configuration["Airtable:BaseId"];
    var outputDirectory = configuration["ImageDownload:OutputDirectory"] ?? "images";

    if (string.IsNullOrEmpty(airtableApiKey) || string.IsNullOrEmpty(airtableBaseId))
    {
      Console.WriteLine("Error: Airtable ApiKey and BaseId must be configured in appsettings.json");
      return 0;
    }

    Console.WriteLine("Starting Airtable Image Downloader...");
    Console.WriteLine($"Output Directory: {outputDirectory}");
    Console.WriteLine();

    try
    {
      // Process ARTWORK_IMAGE table
      int downloaded = await ProcessTable(
        airtableApiKey,
        airtableBaseId,
        "ARTWORK_IMAGE",
        "artwork",
        outputDirectory);

      Console.WriteLine();

      // Process ARCHIVE_IMAGE table
      downloaded += await ProcessTable(
        airtableApiKey,
        airtableBaseId,
        "ARCHIVE_IMAGE",
        "archive",
        outputDirectory);

      Console.WriteLine();
      Console.WriteLine("Image download completed successfully!");
      return downloaded;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
      Console.WriteLine($"Stack trace: {ex.StackTrace}");
      return 0;
    }
  }

  static async Task<int> ProcessTable(
    string apiKey,
    string baseId,
    string tableName,
    string prefix,
    string outputDirectory)
  {
    Console.WriteLine($"Processing table: {tableName}");
    Console.WriteLine(new string('=', 60));

    // Create subdirectory for this table
    var tableDirectory = Path.Combine(outputDirectory, prefix);
    Directory.CreateDirectory(tableDirectory);

    // Fetch records from Airtable
    var records = await FetchAirtableRecords(apiKey, baseId, tableName);
    records.Sort((a, b) => string.Compare(
      (a["fields"] as JObject)?["ID"]?.Value<string>(),
      (b["fields"] as JObject)?["ID"]?.Value<string>(),
      StringComparison.Ordinal));
    Console.WriteLine($"Fetched {records.Count} records from {tableName}");

    int totalFiles = 0;
    int downloadedFiles = 0;
    int skippedFiles = 0;
    int skippedSets = 0;
    bool lastLineWasInPlace = false;

    foreach (var record in records)
    {
      var fields = record["fields"] as JObject;
      var recordId = fields?["ID"]?.Value<string>();

      if (fields == null || string.IsNullOrEmpty(recordId))
        continue;

      // Look for attachment fields (Airtable stores images as attachments)
      foreach (var field in fields.Properties())
      {
        if (field.Value is JArray attachments && attachments.Count > 0)
        {
          // Check if this is an attachment field (has url property)
          var firstItem = attachments.FirstOrDefault() as JObject;
          if (firstItem?["url"] != null)
          {
            foreach (var attachment in attachments.Cast<JObject>())
            {
              var url = attachment["url"]?.ToString();
              var filename = attachment["filename"]?.ToString();

              if (string.IsNullOrEmpty(url))
                continue;

              // Get file extension from filename
              var extension = Path.GetExtension(filename ?? ".jpg");
              if (string.Compare(extension, ".jpeg", true) == 0 ||
                  string.Compare(extension, ".heic", true) == 0)
                extension = ".jpg";

              var baseName = $"{prefix}_{recordId}".ToLower();

              // Build the full set of (sizeName, path, downloadUrl) for this attachment
              var sizes = new List<(string sizeName, string path, string dlUrl)>
              {
                ("full", Path.Combine(tableDirectory, $"{baseName}_full{extension}"), url)
              };

              var thumbnails = attachment["thumbnails"] as JObject;
              if (thumbnails != null)
              {
                foreach (var thumbnail in thumbnails.Properties())
                {
                  var thumbObj = thumbnail.Value as JObject;
                  var thumbUrl = thumbObj?["url"]?.ToString();
                  if (!string.IsNullOrEmpty(thumbUrl))
                    sizes.Add((thumbnail.Name, Path.Combine(tableDirectory, $"{baseName}_{thumbnail.Name}{extension}"), thumbUrl));
                }
              }

              totalFiles += sizes.Count;

              // Process each size, collect results
              var setDownloaded = new List<string>();
              var setFailed = new List<(string sizeName, string message)>();

              foreach (var (sizeName, path, dlUrl) in sizes)
              {
                if (File.Exists(path))
                {
                  skippedFiles++;
                }
                else
                {
                  try
                  {
                    await DownloadImage(dlUrl, path);
                    downloadedFiles++;
                    setDownloaded.Add(sizeName);
                  }
                  catch (Exception ex)
                  {
                    setFailed.Add((sizeName, ex.Message));
                  }
                }
              }

              // Print one summary line per set
              if (setDownloaded.Count == 0 && setFailed.Count == 0)
              {
                // All files in this set already existed
                skippedSets++;
                Console.Write($"\r  ⊘ {skippedSets} sets already exist   ");
                lastLineWasInPlace = true;
              }
              else
              {
                if (lastLineWasInPlace) { Console.WriteLine(); lastLineWasInPlace = false; }
                if (setDownloaded.Count > 0)
                  Console.WriteLine($"  ✓ {baseName}: {string.Join(", ", setDownloaded)}");
                foreach (var (sizeName, msg) in setFailed)
                  Console.WriteLine($"  ✗ {baseName}_{sizeName}: {msg}");
              }
            }
          }
        }
      }
    }

    if (lastLineWasInPlace) Console.WriteLine();
    int failedFiles = totalFiles - downloadedFiles - skippedFiles;
    Console.WriteLine($"Total files:  {totalFiles}");
    Console.WriteLine($"Downloaded:   {downloadedFiles}");
    Console.WriteLine($"Skipped:      {skippedFiles}");
    if (failedFiles > 0)
      Console.WriteLine($"Failed:       {failedFiles}");
    return downloadedFiles;
  }

  static async Task<List<JObject>> FetchAirtableRecords(
    string apiKey,
    string baseId,
    string tableName)
  {
    var records = new List<JObject>();
    string? offset = null;

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    do
    {
      var url = $"https://api.airtable.com/v0/{baseId}/{Uri.EscapeDataString(tableName)}";

      if (!string.IsNullOrEmpty(offset))
      {
        url += $"?offset={offset}";
      }

      var response = await httpClient.GetAsync(url);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var json = JObject.Parse(content);

      var recordsArray = json["records"] as JArray;
      if (recordsArray != null)
      {
        foreach (var record in recordsArray)
        {
          if (record is JObject jobject)
          {
            records.Add(jobject);
          }
        }
      }

      offset = json["offset"]?.ToString();
    } while (!string.IsNullOrEmpty(offset));

    return records;
  }

  static async Task DownloadImage(string url, string outputPath)
  {
    using var httpClient = new HttpClient();
    var imageBytes = await httpClient.GetByteArrayAsync(url);
    await File.WriteAllBytesAsync(outputPath, imageBytes);
  }
}
