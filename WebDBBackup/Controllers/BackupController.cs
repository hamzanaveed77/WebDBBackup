using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Data.SqlClient;
using System.Data;
using WebDBBackup.Models;
using System.IO.Compression;
using System.IO;



namespace WebDBBackup.Controllers
{
    public class BackupController : Controller
    {
        private readonly IConfiguration _configuration;

        public BackupController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            List<DatabaseModel> databases = GetDatabaseList();
            return View(databases);
        }

        [HttpPost]
        public async Task<IActionResult> BackupAndUploadToDrive(List<DatabaseModel> selectedDatabases)
        {
            try
            {
                if (selectedDatabases == null || !selectedDatabases.Any(db => db.IsSelected))
                {
                    throw new Exception("No databases selected for backup. Please select at least one database.");
                }

                var backupDetails = new List<BackupDetails>(); 

                foreach (var db in selectedDatabases)
                {
                    if (db.IsSelected)
                    {
                        string connectionString = _configuration.GetConnectionString("MyDatabaseConnection");
                        string backupDestination = @"D:\Backup\";
                        string backupFilePath = $"{backupDestination}{db.Name}.bak";
                        string zipFilePath = $"{backupDestination}{db.Name}.zip";
                        string backupQuery = $"BACKUP DATABASE {db.Name} TO DISK='{backupFilePath}'";

                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            using (SqlCommand command = new SqlCommand(backupQuery, connection))
                            {
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        using (var zip = System.IO.Compression.ZipFile.Open(zipFilePath, System.IO.Compression.ZipArchiveMode.Create))
                        {
                            zip.CreateEntryFromFile(backupFilePath, Path.GetFileName(backupFilePath));
                        }

                        System.IO.File.Delete(backupFilePath);

                        string driveLink = await UploadBackupToDrive(zipFilePath);

                        backupDetails.Add(new BackupDetails
                        {
                            DatabaseName = db.Name,
                            DriveLink = driveLink
                        });
                    }
                }

                return View("BackupDetailsView", backupDetails);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message; 
                return RedirectToAction(nameof(Index)); 
            }
        }


        private List<DatabaseModel> GetDatabaseList()
        {
            List<DatabaseModel> databases = new List<DatabaseModel>();

            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("MyDatabaseConnection")))
            {
                connection.Open();
                DataTable dt = connection.GetSchema("Databases");

                foreach (DataRow row in dt.Rows)
                {
                    string databaseName = row.Field<string>("database_name");

                    if (databaseName != "master" && databaseName != "tempdb" && databaseName != "model" && databaseName != "msdb")
                    {
                        databases.Add(new DatabaseModel
                        {
                            Name = databaseName,
                            IsSelected = false
                        });
                    }
                }
            }

            return databases;
        }

/*        private static string[] Scopes = { DriveService.Scope.Drive };
*/        
        private async Task<string> UploadBackupToDrive(string filePath)
        {
            /*UserCredential credential;
            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }*/

            

                var serviceAccountKeyFile = "./WebAPIHamzah.json";
                var credential = GoogleCredential.FromFile(serviceAccountKeyFile)
                                    .CreateScoped(DriveService.ScopeConstants.Drive);


            string folderId = "14NFYKlfOUU2JwYBNbb2TDW5zXFr6VN2z";

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Google Drive API",
            });

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(filePath),
                Parents = new List<string> { folderId },
            };
            FilesResource.CreateMediaUpload request;
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
                request.Fields = "id";
                await request.UploadAsync();
            }
            var file = request.ResponseBody;
            var driveLink = $"https://drive.google.com/open?id={file.Id}\n";
            Console.WriteLine($"Link Of The Uploaded File: {driveLink}");

            System.IO.File.Delete(filePath);
            return driveLink;
        }

        private async Task<DriveService> GetDriveService()
        {
            /*UserCredential credential;
            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { DriveService.Scope.Drive },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }*/

            var serviceAccountKeyFile = "./WebAPIHamzah.json";
            var credential = GoogleCredential.FromFile(serviceAccountKeyFile)
                                .CreateScoped(DriveService.ScopeConstants.Drive);

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Google Drive API",
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUploadedZipFiles()
        {
            /*try
            {
                string TargetFolder = "14NFYKlfOUU2JwYBNbb2TDW5zXFr6VN2z";

                var service = await GetDriveService();

                var listRequest = service.Files.List();
                listRequest.Q = "mimeType='application/zip'";
                var files = await listRequest.ExecuteAsync();

                if (files.Files == null || !files.Files.Any())
                {
                    TempData["DeleteMessage"] = "No files found to delete from Drive.";
                }
                else
                {
                    foreach (var file in files.Files)
                    {
                        await service.Files.Delete(file.Id).ExecuteAsync();
                        Console.WriteLine($"Deleted file: {file.Name}");
                    }

                    TempData["DeleteMessage"] = "All files deleted from Drive.";
                }
                return RedirectToAction(nameof(Index)); 

            }*/
            try
            {
                string targetFolderId = "14NFYKlfOUU2JwYBNbb2TDW5zXFr6VN2z"; 

                var service = await GetDriveService();

                var fileListRequest = service.Files.List();
                fileListRequest.Q = $"'{targetFolderId}' in parents and mimeType='application/zip'";
                var files = await fileListRequest.ExecuteAsync();

                if (files.Files == null || !files.Files.Any())
                {
                    TempData["DeleteMessage"] = "No zip files found to delete from the target folder.";
                }
                else
                {
                    foreach (var file in files.Files)
                    {
                        await service.Files.Delete(file.Id).ExecuteAsync();
                        Console.WriteLine($"Deleted file: {file.Name} from the target folder.");
                    }

                    TempData["DeleteMessage"] = "All files deleted from Drive.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

    }
}
