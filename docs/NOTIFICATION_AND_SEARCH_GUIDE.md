# Real-time Notifications & Enhanced Search - Implementation Guide

This document provides instructions for using the newly implemented real-time notification system and enhanced search interface.

## 🔔 Real-time Notification System (PROMPT 0.4)

### Overview
A complete real-time notification system using SignalR for instant updates, similar to Slack/Teams.

### Features Implemented
- ✅ SignalR Hub for real-time communication
- ✅ Server-side notification service with persistence
- ✅ Client-side SignalR integration
- ✅ NotificationCenter UI component (bell icon with badge)
- ✅ Notification types: Document processed, Comments, Mentions, System alerts, Task completed
- ✅ User preferences for notification types
- ✅ Sound and desktop notification support
- ✅ 30-day retention policy with auto-cleanup
- ✅ REST API endpoints for notification management

### Database Setup

**1. Run the migration script:**
```sql
-- Execute this script on your SQL Server database
-- Location: docs/database/migrations/04_add_notifications.sql
```

The script creates:
- `Notifications` table
- `NotificationPreferences` table
- `sp_CleanupOldNotifications` stored procedure

**2. Schedule the cleanup job (optional):**
```sql
-- Run cleanup weekly via SQL Server Agent or Hangfire
EXEC sp_CleanupOldNotifications
```

### Integration Steps

#### 1. Add NotificationCenter to Main Layout

Edit `DocN.Client/Components/Layout/MainLayout.razor` to include the NotificationCenter:

```razor
@using DocN.Client.Components.Shared

<div class="page">
    <div class="sidebar">
        <!-- existing sidebar content -->
    </div>

    <main>
        <div class="top-row px-4">
            <!-- existing header content -->
            
            <!-- Add Notification Center here -->
            <NotificationCenter />
            
            <a href="/login">Login</a>
        </div>

        <article class="content px-4">
            @Body
        </article>
    </main>
</div>
```

#### 2. Initialize Notification Service on Login

In your authentication logic (e.g., after successful login), initialize the notification service:

```csharp
@inject NotificationClientService NotificationService
@inject AuthenticationStateProvider AuthenticationStateProvider

protected override async Task OnInitializedAsync()
{
    var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
    var user = authState.User;
    
    if (user.Identity?.IsAuthenticated == true)
    {
        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await NotificationService.StartAsync(userId);
        }
    }
}
```

#### 3. Include JavaScript Files

Add to `DocN.Client/Components/App.razor` or your main layout:

```html
<script src="js/notifications.js"></script>
```

### Usage Examples

#### Sending a Notification (Server-side)

```csharp
// Inject the service
private readonly INotificationService _notificationService;

// Send notification
await _notificationService.CreateNotificationAsync(
    userId: "user-id-here",
    type: NotificationTypes.DocumentProcessed,
    title: "Documento Elaborato",
    message: "Il tuo documento Report.pdf è pronto",
    link: "/documents/123",
    icon: "document",
    isImportant: false
);
```

#### Notification Types

```csharp
// Available types (from NotificationTypes class)
NotificationTypes.DocumentProcessed  // 📄 Document ready
NotificationTypes.NewComment        // 💬 New comment
NotificationTypes.Mention           // 👤 User mentioned
NotificationTypes.SystemAlert       // ⚠️ System alert
NotificationTypes.TaskCompleted     // ✅ Task done
```

#### Managing User Preferences

```csharp
// Get user preferences
var preferences = await _notificationService.GetOrCreatePreferenceAsync(userId);

// Update preferences
preferences.EnableSound = false;
preferences.EnableDocumentProcessed = true;
preferences.EmailDigestFrequency = "daily";
await _notificationService.UpdatePreferenceAsync(preferences);
```

### API Endpoints

All endpoints require authentication and are prefixed with `/api/notifications`:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Get user's notifications (supports filtering) |
| GET | `/unread-count` | Get count of unread notifications |
| POST | `/mark-read/{id}` | Mark specific notification as read |
| POST | `/mark-all-read` | Mark all notifications as read |
| DELETE | `/{id}` | Delete a notification |
| GET | `/preferences` | Get user's notification preferences |
| PUT | `/preferences` | Update notification preferences |

**Example API Calls:**

```bash
# Get notifications (unread only)
curl -X GET "https://localhost:5211/api/notifications?unreadOnly=true" \
  -H "Authorization: Bearer {token}"

# Mark notification as read
curl -X POST "https://localhost:5211/api/notifications/mark-read/123" \
  -H "Authorization: Bearer {token}"
```

### SignalR Connection

The SignalR hub is available at: `/hubs/notifications`

**Client Methods (called by server):**
- `ReceiveNotification(notification)` - New notification received
- `NotificationMarkedAsRead(notificationId)` - Notification marked as read

**Server Methods (called by client):**
- `MarkAsRead(notificationId)` - Mark notification as read
- `GetUnreadCount()` - Request unread count

---

## 🔍 Enhanced Search Interface (PROMPT 0.5)

### Overview
A powerful search interface with advanced filtering, document preview, and voice search capabilities.

### Features Implemented
- ✅ Enhanced SearchBar with autocomplete and voice search
- ✅ FilterPanel with comprehensive filter options
- ✅ SearchResultCard with highlighted matches
- ✅ DocumentPreview modal for quick viewing
- ✅ Voice search using Web Speech API
- ✅ Recent searches with localStorage
- ✅ Responsive design for all screen sizes

### Components Created

#### 1. SearchBar Component
Location: `DocN.Client/Components/Search/SearchBar.razor`

**Features:**
- Search icon and clear button
- Voice search button (microphone icon)
- Recent searches dropdown
- Autocomplete suggestions
- Keyboard navigation (Enter to search, Escape to close)

**Usage:**
```razor
<SearchBar SearchQuery="@searchQuery"
          SearchQueryChanged="@((value) => searchQuery = value)"
          OnSearch="@HandleSearch"
          EnableVoiceSearch="true"
          EnableAutocomplete="true" />

@code {
    private string searchQuery = "";
    
    private async Task HandleSearch(string query)
    {
        // Perform search with the query
    }
}
```

#### 2. FilterPanel Component
Location: `DocN.Client/Components/Search/FilterPanel.razor`

**Features:**
- File type filters (PDF, Word, Excel, PowerPoint, Text, Images)
- Date range picker with presets
- File size range (min-max MB)
- Author selection
- Tag filters
- Status filters (draft/published/archived)
- Category selection
- Reset all filters button

**Usage:**
```razor
<FilterPanel Filters="@currentFilters"
            OnFiltersChanged="@HandleFiltersChanged"
            IsOpen="@showFilters" />

@code {
    private SearchFilterDto currentFilters = new();
    private bool showFilters = true;
    
    private async Task HandleFiltersChanged(SearchFilterDto filters)
    {
        currentFilters = filters;
        // Apply filters to search
    }
}
```

#### 3. SearchResultCard Component
Location: `DocN.Client/Components/Search/SearchResultCard.razor`

**Features:**
- Colored file type icon
- Document title with highlighted search terms
- Content snippet with highlights
- Metadata (author, date, size, score)
- Action buttons (Open, Preview, Add to Workspace)

**Usage:**
```razor
@foreach (var result in searchResults)
{
    <SearchResultCard Document="@result"
                     SearchQuery="@searchQuery"
                     OnPreview="@HandlePreview"
                     OnOpen="@HandleOpen"
                     OnAddToWorkspace="@HandleAddToWorkspace" />
}
```

#### 4. DocumentPreview Component
Location: `DocN.Client/Components/Document/DocumentPreview.razor`

**Features:**
- Modal dialog for quick preview
- Shows first 3 pages (PDF) or 500 characters (text)
- Highlighted search terms in preview
- Document metadata footer
- "Open Full Document" button

**Usage:**
```razor
<DocumentPreview DocumentId="@selectedDocId"
                SearchQuery="@searchQuery"
                IsOpen="@showPreview"
                OnClose="@(() => showPreview = false)"
                OnOpenFull="@HandleOpenFull" />
```

### JavaScript Files

#### voice-search.js
Location: `DocN.Client/wwwroot/js/voice-search.js`

**Functions:**
- `isVoiceRecognitionSupported()` - Check browser support
- `startVoiceRecognition()` - Start voice input (returns Promise<string>)

**Browser Support:**
- Chrome/Edge: ✅ Full support
- Firefox: ⚠️ Limited support
- Safari: ⚠️ Limited support

### Integration Example

**Complete Search Page:**

```razor
@page "/search"
@using DocN.Data.DTOs
@using DocN.Data.Models

<div class="search-page">
    <div class="search-header">
        <SearchBar SearchQuery="@searchQuery"
                  SearchQueryChanged="@((value) => searchQuery = value)"
                  OnSearch="@PerformSearch" />
    </div>
    
    <div class="search-body">
        <FilterPanel Filters="@filters"
                    OnFiltersChanged="@ApplyFilters"
                    IsOpen="@showFilters" />
        
        <div class="search-results">
            @if (isSearching)
            {
                <FluentProgressRing />
            }
            else if (results.Any())
            {
                <div class="results-header">
                    <span>@results.Count risultati trovati</span>
                    <div class="view-toggle">
                        <!-- Grid/List view toggle -->
                    </div>
                </div>
                
                @foreach (var doc in results)
                {
                    <SearchResultCard Document="@doc"
                                     SearchQuery="@searchQuery"
                                     OnPreview="@ShowPreview"
                                     OnOpen="@OpenDocument" />
                }
            }
            else
            {
                <div class="no-results">
                    <p>Nessun risultato trovato</p>
                </div>
            }
        </div>
    </div>
    
    <DocumentPreview DocumentId="@previewDocId"
                    SearchQuery="@searchQuery"
                    IsOpen="@showPreview"
                    OnClose="@(() => showPreview = false)" />
</div>

@code {
    private string searchQuery = "";
    private SearchFilterDto filters = new();
    private List<Document> results = new();
    private bool isSearching;
    private bool showFilters = true;
    private bool showPreview;
    private int previewDocId;
    
    private async Task PerformSearch(string query)
    {
        isSearching = true;
        // Call search API with query and filters
        isSearching = false;
    }
    
    private async Task ApplyFilters(SearchFilterDto newFilters)
    {
        filters = newFilters;
        await PerformSearch(searchQuery);
    }
    
    private void ShowPreview(int documentId)
    {
        previewDocId = documentId;
        showPreview = true;
    }
    
    private void OpenDocument(int documentId)
    {
        Navigation.NavigateTo($"/documents/{documentId}");
    }
}
```

### Filter DTO Properties

**SearchFilterDto** (from `DocN.Data.DTOs`):

```csharp
public class SearchFilterDto
{
    public List<string>? FileTypes { get; set; }          // [".pdf", ".docx"]
    public DateTime? DateFrom { get; set; }               // Start date
    public DateTime? DateTo { get; set; }                 // End date
    public double? MinSizeMB { get; set; }                // Min file size
    public double? MaxSizeMB { get; set; }                // Max file size
    public List<string>? Authors { get; set; }            // ["user1", "user2"]
    public List<string>? Tags { get; set; }               // ["important", "draft"]
    public string? Status { get; set; }                   // "published"
    public string? Category { get; set; }                 // "reports"
    public string SortBy { get; set; } = "relevance";     // "relevance", "date", "name"
    public string SortDirection { get; set; } = "desc";   // "asc", "desc"
    public string ViewMode { get; set; } = "list";        // "list", "grid"
}
```

### Styling

All components include scoped CSS files (`.razor.css`) with:
- FluentUI design tokens for theming
- Responsive breakpoints for mobile/tablet/desktop
- Smooth animations and transitions
- Hover and focus states
- Accessibility features

---

## 🚀 Next Steps

### For Notifications:
1. ✅ Run database migration script
2. ✅ Add NotificationCenter to layout
3. ✅ Initialize service on user login
4. ✅ Test real-time notifications
5. Schedule cleanup job (optional)

### For Enhanced Search:
1. ✅ Replace existing search UI with new components
2. ✅ Test voice search functionality
3. ✅ Configure document preview service
4. Optional: Extend SearchController with additional endpoints if needed

## 📝 Notes

- **Browser Compatibility**: Voice search requires modern browsers with Web Speech API support
- **SignalR**: Ensure CORS is properly configured for SignalR connections
- **Performance**: NotificationCenter uses virtualization for large notification lists
- **Security**: All API endpoints require authentication
- **Localization**: UI text is in Italian (Italian language)

## 🐛 Troubleshooting

### Notifications not appearing:
1. Check SignalR connection in browser console
2. Verify user is authenticated
3. Check notification preferences are enabled
4. Ensure backend hub is registered in Program.cs

### Voice search not working:
1. Check browser support: `window.isVoiceRecognitionSupported()`
2. Ensure HTTPS (required for microphone access)
3. Grant microphone permissions in browser

### Search filters not applying:
1. Verify SearchFilterDto is being passed correctly
2. Check browser console for JavaScript errors
3. Ensure filter values are in correct format

---

## 📚 Additional Resources

- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr)
- [Web Speech API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Speech_API)
- [FluentUI Blazor Components](https://www.fluentui-blazor.net/)

---

**Created:** 2026-01-25  
**Version:** 1.0  
**Status:** Ready for Production
