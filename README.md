# Favicon Scraper API

A .NET REST API designed to extract favicons from any website. It parses HTML to find the correct icon link, falls back to standard locations, and supports on-the-fly resizing.

## Features

- **Intelligent Extraction**: Parses HTML to find `<link rel="icon">` tags, handling relative and absolute URLs.
- **Automatic Fallback**: If no icon is explicitly defined, gracefully falls back to the standard `/favicon.ico`.
- **Dynamic Resizing**: Request favicons in specific sizes (e.g., 16, 32, 64 px) using the `size` parameter. Powered by SkiaSharp for high-quality scaling.
- **Performance Optimized**: 
  - **Caching**: Implements in-memory caching (24-hour duration) to minimize external requests and improve response times.
  - **Rate Limiting**: Built-in protection (10 requests per 10 seconds) to prevent abuse.

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (Version 8.0 or later recommended)

### Installation

1. Clone the repository:
   ```bash
   git clone <repository-url>
   ```
2. Navigate to the project directory:
   ```bash
   cd FaviconScraper.Api
   ```
3. Restore dependencies:
   ```bash
   dotnet restore
   ```

### Running the Application

Run the application using the dotnet CLI:

```bash
dotnet run
```

The API will start (typically on `https://localhost:7136` or `http://localhost:5251`, check your console output).

## API Documentation

### 1. Get Favicon (Original Size)
Fetches the favicon for the specified URL in its original resolution.

**Endpoint:**
`GET /api/favicon/{url}`

**Example:**
```
GET /api/favicon/google.com
GET /api/favicon/https://github.com
```

### 2. Get Resized Favicon
Fetches and resizes the favicon to the specified square dimension (width = height).

**Endpoint:**
`GET /api/favicon/{size}/{url}`

**Parameters:**
- `size` (int): The target width/height in pixels.
- `url` (string): The target website URL.

**Example:**
```
GET /api/favicon/64/google.com
```

## Built With

- [ASP.NET Core](https://asp.net/) - The web framework used.
- [HtmlAgilityPack](https://html-agility-pack.net/) - For parsing HTML and extracting icon links.
- [SkiaSharp](https://github.com/mono/SkiaSharp) - For cross-platform image processing and resizing.

## License

[MIT](LICENSE)
