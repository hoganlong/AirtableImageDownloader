using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

class Program
{
  static async Task Main(string[] args)
  {
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
      return;
    }

    Console.WriteLine("Starting Airtable Image Downloader...");
    Console.WriteLine($"Output Directory: {outputDirectory}");
    Console.WriteLine();

    try
    {
      // Process ARTWORK_IMAGE table
      await ProcessTable(
        airtableApiKey,
        airtableBaseId,
        "ARTWORK_IMAGE",
        "artwork",
        outputDirectory);

      Console.WriteLine();

      // Process ARCHIVE_IMAGE table
      await ProcessTable(
        airtableApiKey,
        airtableBaseId,
        "ARCHIVE_IMAGE",
        "archive",
        outputDirectory);

      Console.WriteLine();
      Console.WriteLine("Image download completed successfully!");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
      Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
  }

  static async Task ProcessTable(
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
    Console.WriteLine($"Fetched {records.Count} records from {tableName}");

    int totalImages = 0;
    int downloadedImages = 0;
    int skippedImages = 0;

    foreach (var record in records)
    {
     // var recordId = record["id"]?.ToString();
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
            // Process each attachment
            foreach (var attachment in attachments.Cast<JObject>())
            {
              totalImages++;
              var url = attachment["url"]?.ToString();
              var filename = attachment["filename"]?.ToString();
              var width = attachment["width"]?.ToString();
              var height = attachment["height"]?.ToString();

              if (string.IsNullOrEmpty(url))
                continue;

              // Determine size string
              var sizeString = "full";
            //  if (!string.IsNullOrEmpty(width) && !string.IsNullOrEmpty(height))
            //  {
            //    sizeString = $"{width}x{height}";
            //  }

              // Get file extension from filename
              var extension = Path.GetExtension(filename ?? ".jpg");
              if (String.Compare(extension, ".jpeg", true) == 0 ||
                  String.Compare(extension, ".heic", true) == 0)
              {
                extension = ".jpg"; // Normalize jpeg/heic to jpg
              }

              // Build output filename: prefix_recordId_size.ext
              string v = $"{prefix}_{recordId}_{sizeString}{extension}".ToLower();
              var outputFilename = v;
              var outputPath = Path.Combine(tableDirectory, outputFilename);

              // Check if file already exists
              if (File.Exists(outputPath))
              {
                skippedImages++;
                Console.WriteLine($"  ⊘ Skipped (exists): {outputFilename}");
              }
              else
              {
                // Download the image
                try
                {
                  await DownloadImage(url, outputPath);
                  downloadedImages++;
                  Console.WriteLine($"  ✓ Downloaded: {outputFilename}");
                }
                catch (Exception ex)
                {
                  Console.WriteLine($"  ✗ Failed to download {outputFilename}: {ex.Message}");
                }
              }

              // Also process thumbnails if available
              var thumbnails = attachment["thumbnails"] as JObject;
              if (thumbnails != null)
              {
                foreach (var thumbnail in thumbnails.Properties())
                {
                  totalImages++;
                  var thumbObj = thumbnail.Value as JObject;
                  var thumbUrl = thumbObj?["url"]?.ToString();
                  var thumbWidth = thumbObj?["width"]?.ToString();
                  var thumbHeight = thumbObj?["height"]?.ToString();

                  if (string.IsNullOrEmpty(thumbUrl))
                    continue;

                  // Size string for thumbnail
                  var thumbSizeString = thumbnail.Name; // small, large, etc.
              //    if (!string.IsNullOrEmpty(thumbWidth) && !string.IsNullOrEmpty(thumbHeight))
              //    {
              //      thumbSizeString = $"{thumbnail.Name}_{thumbWidth}x{thumbHeight}";
              //    }

                  var thumbFilename = $"{prefix}_{recordId}_{thumbSizeString}{extension}".ToLower();
                  var thumbPath = Path.Combine(tableDirectory, thumbFilename);

                  // Check if thumbnail already exists
                  if (File.Exists(thumbPath))
                  {
                    skippedImages++;
                    Console.WriteLine($"  ⊘ Skipped (exists): {thumbFilename}");
                    continue;
                  }

                  try
                  {
                    await DownloadImage(thumbUrl, thumbPath);
                    downloadedImages++;
                    Console.WriteLine($"  ✓ Downloaded: {thumbFilename}");
                  }
                  catch (Exception ex)
                  {
                    Console.WriteLine($"  ✗ Failed to download {thumbFilename}: {ex.Message}");
                  }
                }
              }
            }
          }
        }
      }
    }

    Console.WriteLine($"Total images found: {totalImages}");
    Console.WriteLine($"Successfully downloaded: {downloadedImages}");
    Console.WriteLine($"Skipped (already exist): {skippedImages}");
    Console.WriteLine($"Failed: {totalImages - downloadedImages - skippedImages}");
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
