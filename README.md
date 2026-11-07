# AirtableImageDownloader

A .NET console application that downloads images from specific Airtable tables and stores them locally with a structured naming convention.

## Purpose

This application connects to an Airtable base and downloads all images from two specific tables:
- `ARTWORK_IMAGE` - Images related to artwork
- `ARCHIVE_IMAGE` - Images from archives

The images are organized into subdirectories and named systematically for easy reference.

## Related Projects

This project is similar to the `AirtableToPostgres` project in the parent directory, sharing the same Airtable connection credentials and database. However, instead of syncing data to PostgreSQL, this project focuses on downloading and organizing image files.

## Airtable Connection

### Configuration
The application connects to Airtable using credentials stored in `appsettings.json`:

```json
{
  "Airtable": {
    "ApiKey": "YOUR_AIRTABLE_API_KEY_HERE",
    "BaseId": "appnGXIULYw1ZA6bF"
  },
  "ImageDownload": {
    "OutputDirectory": "images"
  }
}
```

- **ApiKey**: Personal Access Token for Airtable API authentication
- **BaseId**: The unique identifier for the Airtable base (same as used in AirtableToPostgres)
- **OutputDirectory**: Root directory where images will be saved (default: "images")

### Database Connection
The same PostgreSQL database used in AirtableToPostgres is referenced:
```
Host: <cluster-endpoint>.rds.amazonaws.com
Database: keithlongarchive
```

However, this project does NOT write to the database - it only downloads images from Airtable.

## How It Works

### 1. Fetching Records
The application uses the Airtable REST API to fetch all records from each target table:
- Makes paginated GET requests to `https://api.airtable.com/v0/{baseId}/{tableName}`
- Handles pagination using offset tokens
- Retrieves all fields for each record

### 2. Finding Images
For each record, the application:
- Examines all fields looking for attachment fields
- Identifies attachment fields by checking for objects with a `url` property
- Extracts both full-size images and thumbnails

### 3. Image Processing
For each image found:
- Extracts the record ID, image URL, dimensions, and file extension
- Generates a structured filename based on the naming convention
- Checks if the file already exists (skip if present)
- Downloads the image if not already downloaded
- Processes all thumbnail variants (small, large, etc.)

### 4. File Organization
Images are saved in subdirectories based on the source table:
```
images/
├── artwork/          # Images from ARTWORK_IMAGE table
│   ├── artwork_rec123ABC_1920x1080.jpg
│   ├── artwork_rec123ABC_small_200x150.jpg
│   └── artwork_rec123ABC_large_1000x750.jpg
└── archive/          # Images from ARCHIVE_IMAGE table
    ├── archive_rec456DEF_2048x1536.png
    └── archive_rec456DEF_small_256x192.png
```

## File Naming Convention

Files are named using the following pattern:
```
{prefix}_{recordId}_{size}{extension}
```

### Components:
- **prefix**: Table identifier
  - `artwork` for ARTWORK_IMAGE table
  - `archive` for ARCHIVE_IMAGE table
- **recordId**: The Airtable record ID (e.g., `rec123ABC`)
- **size**: Dimension information
  - For full-size: `{width}x{height}` (e.g., `1920x1080`)
  - For thumbnails: `{thumbnailName}_{width}x{height}` (e.g., `small_200x150`, `large_1000x750`)
  - Fallback: `full` if dimensions are not available
- **extension**: Original file extension (e.g., `.jpg`, `.png`, `.gif`)

### Examples:
```
artwork_recA1B2C3D4_1920x1080.jpg          # Full-size artwork image
artwork_recA1B2C3D4_small_200x150.jpg      # Small thumbnail
artwork_recA1B2C3D4_large_1000x750.jpg     # Large thumbnail
archive_recX9Y8Z7W6_2048x1536.png          # Full-size archive image
archive_recX9Y8Z7W6_small_256x192.png      # Small thumbnail
```

## Features

### Resume Support
The application checks if files already exist before downloading:
- **Skips** files that are already present on disk
- Allows the program to be interrupted and resumed
- Avoids re-downloading existing images
- Reports skipped files in output statistics

### Progress Reporting
During execution, the application shows:
- Current table being processed
- Each file being downloaded, skipped, or failed
- Summary statistics for each table:
  - Total images found
  - Successfully downloaded
  - Skipped (already exist)
  - Failed downloads

### Error Handling
- Validates that API key and Base ID are configured
- Continues processing if individual downloads fail
- Reports specific errors for failed downloads
- Completes successfully even if some images fail

## Running the Application

### Prerequisites
- .NET 10.0 SDK installed
- Network access to Airtable API
- Valid Airtable credentials in `appsettings.json`

### Build
```bash
cd AirtableImageDownloader
dotnet build
```

### Run
```bash
cd AirtableImageDownloader
dotnet run
```

### Expected Output
```
Starting Airtable Image Downloader...
Output Directory: images

Processing table: ARTWORK_IMAGE
============================================================
Fetched 150 records from ARTWORK_IMAGE
  ✓ Downloaded: artwork_rec123ABC_1920x1080.jpg
  ✓ Downloaded: artwork_rec123ABC_small_200x150.jpg
  ⊘ Skipped (exists): artwork_rec456DEF_1920x1080.jpg
  ...
Total images found: 450
Successfully downloaded: 300
Skipped (already exist): 100
Failed: 50

Processing table: ARCHIVE_IMAGE
============================================================
Fetched 200 records from ARCHIVE_IMAGE
  ✓ Downloaded: archive_recXYZ789_2048x1536.png
  ...
Total images found: 600
Successfully downloaded: 550
Skipped (already exist): 50
Failed: 0

Image download completed successfully!
```

## Project Structure

```
AirtableImageDownloader/
├── Program.cs                    # Main application logic
├── appsettings.json             # Configuration file (API keys, settings)
├── AirtableImageDownloader.csproj  # Project file
├── README.md                    # This documentation
└── images/                      # Output directory (created at runtime)
    ├── artwork/                 # ARTWORK_IMAGE downloads
    └── archive/                 # ARCHIVE_IMAGE downloads
```

## Technical Details

### Dependencies
- **Microsoft.Extensions.Configuration** (10.0.3) - Configuration management
- **Microsoft.Extensions.Configuration.Json** (10.0.3) - JSON configuration support
- **Newtonsoft.Json** (13.0.4) - JSON parsing for Airtable API responses
- **.NET 10.0** - Target framework

### Key Classes and Methods

#### `Main()`
- Entry point
- Loads configuration
- Calls `ProcessTable()` for each target table
- Handles top-level exceptions

#### `ProcessTable()`
- Creates output subdirectory
- Fetches all records from specified table
- Iterates through records looking for attachment fields
- Downloads images and thumbnails
- Tracks statistics (downloaded, skipped, failed)

#### `FetchAirtableRecords()`
- Makes paginated API calls to Airtable
- Handles pagination with offset tokens
- Returns list of all records as JObjects

#### `DownloadImage()`
- Downloads image from URL
- Writes bytes to local file system
- Simple HTTP GET request

### Airtable API Details

**Endpoint**: `https://api.airtable.com/v0/{baseId}/{tableName}`

**Authentication**: Bearer token in Authorization header

**Response Format**:
```json
{
  "records": [
    {
      "id": "recXXXXXXXXXXXXX",
      "fields": {
        "fieldName": [
          {
            "id": "attYYYYYYYYYYYYY",
            "url": "https://dl.airtable.com/...",
            "filename": "image.jpg",
            "size": 123456,
            "type": "image/jpeg",
            "width": 1920,
            "height": 1080,
            "thumbnails": {
              "small": {
                "url": "https://dl.airtable.com/...",
                "width": 200,
                "height": 150
              },
              "large": {
                "url": "https://dl.airtable.com/...",
                "width": 1000,
                "height": 750
              }
            }
          }
        ]
      }
    }
  ],
  "offset": "itrXXXXXXXXXXXXX"
}
```

**Pagination**: Use offset parameter for next page

### Attachment Field Detection
The application automatically detects attachment fields by:
1. Looking for fields containing JArray values
2. Checking if array items have a `url` property
3. Processing all items in the array

This means it works regardless of the field names in Airtable - it discovers attachments dynamically.

## Maintenance Notes

### Adding New Tables
To download images from additional tables, add another `ProcessTable()` call in `Main()`:

```csharp
await ProcessTable(
    airtableApiKey,
    airtableBaseId,
    "NEW_TABLE_NAME",
    "newprefix",
    outputDirectory);
```

### Changing Output Location
Modify `OutputDirectory` in `appsettings.json`:
```json
"ImageDownload": {
  "OutputDirectory": "path/to/output"
}
```

### Changing Naming Convention
Modify the filename generation logic in `ProcessTable()`:
- Line ~112: Full-size image naming
- Line ~149: Thumbnail naming

## Troubleshooting

### "Error: Airtable ApiKey and BaseId must be configured"
- Check that `appsettings.json` exists in the output directory
- Verify ApiKey and BaseId values are not empty

### Downloads Failing
- Check network connectivity
- Verify Airtable API key is still valid
- Check that the base ID is correct
- Ensure you have read access to the tables

### No Images Found
- Verify the table names are correct (case-sensitive)
- Check that tables contain attachment fields
- Confirm records have images uploaded

### Permission Denied Errors
- Ensure write permissions for the output directory
- Check available disk space

## Future Enhancements

Possible improvements:
- Parallel downloads for better performance
- Filtering by specific field names
- Date-based incremental downloads
- Image optimization/resizing
- Duplicate detection by image hash
- Database logging of downloads
- Command-line arguments for table selection
- Configuration for specific attachment fields only
