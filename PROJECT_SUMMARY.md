# Project Summary: AirtableImageDownloader

## Executive Summary

**Purpose**: Download and organize images from Airtable tables (ARTWORK_IMAGE and ARCHIVE_IMAGE) to local file system with structured naming.

**Created**: February 2026
**Framework**: .NET 10.0
**Language**: C#
**Status**: Complete and ready to run

## Key Facts

- **Tables Processed**: 2 (ARTWORK_IMAGE, ARCHIVE_IMAGE)
- **Output Location**: `images/` directory with subdirectories
- **Naming Pattern**: `{prefix}_{recordId}_{size}.{ext}`
- **Resume Capable**: Yes - skips existing files
- **Database Required**: No - file downloads only
- **Related Project**: AirtableToPostgres (syncs data to PostgreSQL)

## File Structure

```
AirtableImageDownloader/
├── Program.cs                 # Main application (222 lines)
├── appsettings.json          # Configuration (API keys, settings)
├── AirtableImageDownloader.csproj  # Project file
├── README.md                 # Complete documentation
├── ARCHITECTURE.md           # System design & technical details
├── QUICKSTART.md            # 5-minute getting started guide
├── PROJECT_SUMMARY.md       # This file
└── images/                  # Output directory (created at runtime)
    ├── artwork/             # ARTWORK_IMAGE downloads
    └── archive/             # ARCHIVE_IMAGE downloads
```

## Documentation Map

| File | Purpose | When to Read |
|------|---------|--------------|
| **QUICKSTART.md** | Fast setup and run | First time running |
| **README.md** | Complete user guide | Understanding features |
| **ARCHITECTURE.md** | Technical design | Development/modification |
| **PROJECT_SUMMARY.md** | Overview (this file) | Quick reference |

## How It Works (Simple Version)

1. **Connect** to Airtable using API key
2. **Fetch** all records from ARTWORK_IMAGE table
3. **Find** image attachments in each record
4. **Download** each image (full size + thumbnails)
5. **Save** as `artwork_{recordId}_{size}.ext`
6. **Repeat** for ARCHIVE_IMAGE table
7. **Skip** any files that already exist

## Example Output

After running, you'll have files like:
```
images/artwork/artwork_rec1A2B3C4_1920x1080.jpg
images/artwork/artwork_rec1A2B3C4_small_200x150.jpg
images/artwork/artwork_rec1A2B3C4_large_1000x750.jpg
images/archive/archive_recX9Y8Z7_2048x1536.png
images/archive/archive_recX9Y8Z7_small_256x192.png
```

## Configuration Required

Only `appsettings.json` needs to be configured:

```json
{
  "Airtable": {
    "ApiKey": "your-api-key",
    "BaseId": "your-base-id"
  },
  "ImageDownload": {
    "OutputDirectory": "images"
  }
}
```

**Current Settings**: Already configured for Keith Long Archive base

## Key Features

### ✓ Resume Support
- Checks if files exist before downloading
- Safe to interrupt and restart
- Won't re-download existing images

### ✓ All Size Variants
- Full-size original images
- Small thumbnails (~200px)
- Large thumbnails (~1000px)
- Any other sizes Airtable provides

### ✓ Automatic Detection
- Finds attachment fields automatically
- No need to specify field names
- Works with any attachment in any field

### ✓ Progress Reporting
```
Processing table: ARTWORK_IMAGE
============================================================
Fetched 150 records from ARTWORK_IMAGE
  ✓ Downloaded: artwork_rec123_1920x1080.jpg
  ⊘ Skipped (exists): artwork_rec456_1920x1080.jpg
  ✗ Failed: artwork_rec789_1920x1080.jpg (Network error)
Total images found: 450
Successfully downloaded: 300
Skipped (already exist): 100
Failed: 50
```

## Running the Application

### First Time
```bash
cd AirtableImageDownloader
dotnet build
dotnet run
```

### Subsequent Runs
```bash
cd AirtableImageDownloader
dotnet run
```

### Expected Duration
- Depends on number of images and network speed
- ~1-5 seconds per image (including thumbnails)
- 100 images = ~5-10 minutes
- Can be interrupted safely

## Integration with AirtableToPostgres

### Similarities
- Both connect to same Airtable base (`appnGXIULYw1ZA6bF`)
- Both use .NET 10.0
- Both handle Keith Long Archive data

### Differences
- **AirtableToPostgres**: Data → PostgreSQL database
- **AirtableImageDownloader**: Images → File system
- No direct integration between projects
- Run independently

### Future Integration Possibilities
- Store image paths in PostgreSQL
- Link images to synced data records
- Coordinate execution (sync data, then images)
- Web app using both data + images

## Technical Stack

### Dependencies
- Microsoft.Extensions.Configuration (10.0.3)
- Microsoft.Extensions.Configuration.Json (10.0.3)
- Newtonsoft.Json (13.0.4)

### Key Technologies
- HttpClient for downloads
- File.Exists() for resume support
- Airtable REST API v0
- JSON parsing with JObject

### Code Statistics
- **Total Lines**: ~222 (Program.cs)
- **Methods**: 4 main methods
- **Classes**: 1 (Program)
- **Complexity**: Low - straightforward sequential processing

## Common Use Cases

### 1. Initial Download
```bash
dotnet run
```
Downloads all images from both tables.

### 2. Update Images
```bash
dotnet run
```
Downloads only new images added since last run.

### 3. Recover from Interruption
```bash
dotnet run
```
Picks up where it left off.

### 4. Change Output Location
Edit `appsettings.json`:
```json
"OutputDirectory": "C:/my/images"
```
Then run: `dotnet run`

## Production Considerations

### Scheduling
- Run as scheduled task (nightly, weekly)
- Windows Task Scheduler or cron job
- Check for new images periodically

### Storage
- Monitor disk space (images can be large)
- Plan for growth (more records = more images)
- Consider cleanup of old/unused images

### Backups
- Backup images/ directory regularly
- Can re-download from Airtable if needed
- Consider cloud storage sync (S3, Azure Blob)

### Performance
- Currently sequential downloads
- Could parallelize for faster processing
- Respect Airtable rate limits (5 req/sec)

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No images downloaded | Verify table names are correct |
| API error | Check API key is valid |
| Permission denied | Ensure write access to output directory |
| Out of disk space | Free up space or change output location |
| Network timeout | Check internet connection, retry |

## Future Enhancements

### Potential Improvements
1. **Parallel Downloads** - Process multiple images simultaneously
2. **Database Integration** - Store download metadata in PostgreSQL
3. **Incremental Sync** - Only fetch records modified since last run
4. **Cloud Upload** - Automatically upload to S3/Azure after download
5. **Image Processing** - Resize, optimize, watermark
6. **CLI Arguments** - Specify tables/output location via command line
7. **Webhook Support** - Trigger on Airtable changes
8. **Progress Bar** - Better visual progress indicator
9. **Retry Logic** - Automatic retry for failed downloads
10. **Logging** - Write detailed logs to file

### Not Planned (Out of Scope)
- Image editing/manipulation
- OCR or image analysis
- Video downloads
- Direct database integration
- Web interface

## Maintenance Notes

### Regular Maintenance
- Check API key expiration
- Monitor disk space usage
- Review failed downloads
- Update .NET version as needed

### Code Modifications
All logic is in single file (`Program.cs`):
- **Lines 1-49**: Main entry point and error handling
- **Lines 51-173**: ProcessTable (core download logic)
- **Lines 175-214**: FetchAirtableRecords (API calls)
- **Lines 216-221**: DownloadImage (file writing)

### Adding New Tables
Add another `ProcessTable()` call in `Main()`:
```csharp
await ProcessTable(
    airtableApiKey,
    airtableBaseId,
    "NEW_TABLE_NAME",
    "newprefix",
    outputDirectory);
```

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Feb 2026 | Initial creation with resume support |

## Contact & Support

- **Documentation**: See README.md for detailed guide
- **Issues**: Check troubleshooting section first
- **Source Code**: Program.cs (fully commented)

## Success Criteria

The application is working correctly when:
- ✓ Both tables are processed without errors
- ✓ Images are saved to correct subdirectories
- ✓ Filenames match expected pattern
- ✓ Existing files are skipped
- ✓ Statistics show successful downloads
- ✓ Images can be opened and viewed

## Quick Reference Commands

```bash
# Build project
dotnet build

# Run application
dotnet run

# Clean build artifacts
dotnet clean

# Restore packages
dotnet restore

# Check .NET version
dotnet --version
```

---

**Last Updated**: February 15, 2026
**Project Location**: `D:\Projects\claudetest\AirtableImageDownloader`
**Framework**: .NET 10.0
**Status**: ✓ Complete and Ready to Use
