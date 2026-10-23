using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TestingSpareManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;

namespace TestingSpareManagement.Controllers
{
    public class MaterialRequestController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MaterialRequestController> _logger;

        public MaterialRequestController(
            IConfiguration configuration,
            ILogger<MaterialRequestController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> Index(
            int pageNumber = 1,
            int pageSize = 25)
        {
            if (pageNumber < 1)
                pageNumber = 1;

            int[] allowedPageSizes = { 10, 25, 50, 100 };

            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 25;

            var model = new MaterialRequestViewModel
            {
                RequestDate = DateTime.Today,
                CurrentPage = pageNumber,
                PageSize = pageSize
            };

            // Load active projects from ProjectMaster
            model.ProjectCodes = await GetActiveProjectCodes();
            ViewBag.SpareDescriptions = await GetMasterConnectorDescriptions();
            model.TestingStations = await GetTestingStations();
            // Select A246f by default if it is active
            model.ProjectCode =
                model.ProjectCodes.FirstOrDefault(p =>
                    string.Equals(
                        p.Value,
                        "A246f",
                        StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?? model.ProjectCodes.FirstOrDefault()?.Value
                ?? "";

            // Load existing material request records
            var (records, totalRecords) =
                await GetMaterialRequests(pageNumber, pageSize);

            model.Requests = records;
            model.TotalRecords = totalRecords;

            if (model.TotalRecords > 0 &&
                pageNumber > model.TotalPages)
            {
                return RedirectToAction(nameof(Index), new
                {
                    pageNumber = model.TotalPages,
                    pageSize = pageSize
                });
            }

            return View(model);
        }



        private async Task<List<string>> GetMasterConnectorDescriptions()
        {
            var descriptions = new List<string>();

            using SqlConnection connection =
                new SqlConnection(GetConnectionString());

            const string query = @"
        SELECT SpareItem
        FROM
        (
            SELECT DISTINCT
                LTRIM(RTRIM(DescriptionPartNo)) AS SpareItem
            FROM dbo.MasterConnector
            WHERE IsActive = 1
              AND DescriptionPartNo IS NOT NULL
              AND LTRIM(RTRIM(DescriptionPartNo)) <> ''

            UNION

            SELECT DISTINCT
                LTRIM(RTRIM(TestItemName)) AS SpareItem
            FROM dbo.EquipmentConsumable
            WHERE IsActive = 1
              AND TestItemName IS NOT NULL
              AND LTRIM(RTRIM(TestItemName)) <> ''
        ) AS SpareItems
        ORDER BY SpareItem;";

            using SqlCommand command = new SqlCommand(query, connection);

            await connection.OpenAsync();

            using SqlDataReader reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string item = reader["SpareItem"]?.ToString()?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(item))
                    descriptions.Add(item);
            }

            return descriptions;
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(MaterialRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload data and return to view
                var (records, totalRecords) = await GetMaterialRequests(1, 25);
                model.Requests = records;
                model.TotalRecords = totalRecords;
                model.ProjectCodes = await GetActiveProjectCodes();
                ViewBag.SpareDescriptions = await GetMasterConnectorDescriptions();
                return View("Index", model);
            }

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.InsertMaterialRequest", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@RequestDate", SqlDbType.Date).Value = model.RequestDate;
            command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode;

            string requestedBy = HttpContext.Session.GetString("EmployeeId") ?? "Unknown";
            command.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = requestedBy;

            command.Parameters.Add("@SpareDescription", SqlDbType.NVarChar, 250).Value = model.SpareDescription;
            command.Parameters.Add("@PartNumber", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(model.PartNumber) ? DBNull.Value : model.PartNumber;
            command.Parameters.Add("@Brand", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(model.Brand) ? DBNull.Value : model.Brand;
            command.Parameters.Add("@TargetMachine", SqlDbType.NVarChar, 50).Value = model.TargetMachine;
            command.Parameters.Add("@RequestedQuantity", SqlDbType.Int).Value = model.RequestedQuantity;
            command.Parameters.Add("@PriorityLevel", SqlDbType.NVarChar, 50).Value = model.PriorityLevel;
            command.Parameters.Add("@Justification", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(model.Justification) ? DBNull.Value : model.Justification;
            command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = requestedBy;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            try
            {
                await SendMaterialRequestEmailAsync(model, requestedBy);

                TempData["SuccessMessage"] =
                    "Material request submitted successfully and email notification sent.";
            }
            catch (Exception ex)
            {
                // Do not lose the saved request if email delivery fails.
                _logger.LogError(
                    ex,
                    "Material request saved, but email notification failed.");

                TempData["SuccessMessage"] =
                    "Material request submitted successfully.";

                TempData["EmailWarning"] =
                    "The request was saved, but the email could not be sent. " +
                    "Please contact the system administrator.";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest();

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.DeleteMaterialRequest", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                HttpContext.Session.GetString("EmployeeName") ?? "System";

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = "Material request deleted successfully.";
            return RedirectToAction(nameof(Index));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            if (id <= 0)
                return BadRequest();

            using SqlConnection connection =
                new SqlConnection(GetConnectionString());

            await connection.OpenAsync();

            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // STEP 1: Approve the Material Request.
                string updateQuery = @"
            UPDATE dbo.MaterialRequest
            SET Status = 'Approved',
                AdminActions = 'Processed'
            WHERE Id = @Id
              AND (
                    Status IN ('Pending', 'Pending Admin Approval')
                    OR Status IS NULL
                    OR LTRIM(RTRIM(Status)) = ''
                  );";

                using (SqlCommand updateCommand =
                    new SqlCommand(updateQuery, connection, transaction))
                {
                    updateCommand.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                    int rowsUpdated =
                        await updateCommand.ExecuteNonQueryAsync();

                    if (rowsUpdated != 1)
                    {
                        await transaction.RollbackAsync();

                        TempData["ErrorMessage"] =
                            "Request was not found or has already been processed.";

                        return RedirectToAction(nameof(Index));
                    }
                }

                // STEP 2: Insert the approved request into the PR/PO ledger.
                string insertQuery = @"
            INSERT INTO dbo.PrPoTracker
            (
                MaterialRequestId,
                ReqId,
                RequestedBy,
                SpareDescription,
                PartNumber,
                Brand,
                TargetMachine,
                PriorityLevel,
                Justification,
                RequisitionDate,
                ProjectCode,
                PrNumber,
                PoStatus,
                PurchaseOriginRegion,
                BuyerName,
                AssetTarget,
                Quantity,
                UnitPrice,
                Subtotal,
                TaxPercent,
                TaxAmount,
                TotalCost,
                ShipmentTrackingNo,
                DeliveryDate,
                ShipmentStatus,
                Remarks,
                IsActive,
                CreatedBy,
                CreatedDate
            )
            SELECT
                mr.Id,
                mr.ReqId,
                mr.RequestedBy,
                mr.SpareDescription,
                mr.PartNumber,
                mr.Brand,
                mr.TargetMachine,
                mr.PriorityLevel,
                mr.Justification,
                mr.RequestDate,
                mr.ProjectCode,
                mr.ReqId,
                'PR ISSUED',
                'India Purchase_IN',
                'Pending Assignment',
                mr.SpareDescription,
                mr.RequestedQuantity,
                0,
                0,
                0,
                0,
                0,
                NULL,
                NULL,
                NULL,
                mr.Justification,
                1,
                mr.RequestedBy,
                GETDATE()
            FROM dbo.MaterialRequest AS mr
            WHERE mr.Id = @Id
              AND mr.Status = 'Approved'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.PrPoTracker AS p
                  WHERE p.MaterialRequestId = mr.Id
              );";

                using (SqlCommand insertCommand =
                    new SqlCommand(insertQuery, connection, transaction))
                {
                    insertCommand.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                    int rowsInserted =
                        await insertCommand.ExecuteNonQueryAsync();

                    if (rowsInserted != 1)
                    {
                        throw new InvalidOperationException(
                            "The request could not be added to the PR/PO ledger, " +
                            "or a ledger record already exists for this request.");
                    }
                }

                // STEP 3: Save both database changes together.
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "Material request approved and added to the PR/PO ledger successfully.";
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(
                        rollbackEx,
                        "Rollback failed for Material Request {RequestId}.",
                        id);
                }

                _logger.LogError(
                    ex,
                    "Failed to approve Material Request {RequestId} or insert its PR/PO record.",
                    id);

                TempData["ErrorMessage"] =
                    "Approval could not be completed. Please check the application log.";

                return RedirectToAction(nameof(Index));
            }

            // STEP 4: Keep your existing approval email notification.
            try
            {
                await SendDecisionEmailAsync(id, "Approved");

                TempData["EmailSuccessMessage"] =
                    "Approval notification email sent to Saibalaji_M@foxlink.com.";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Request {RequestId} was approved, but the approval email failed.",
                    id);

                TempData["EmailWarning"] =
                    "The request was approved, but the email notification could not be sent.";
            }

            return RedirectToAction(nameof(Index));
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            if (id <= 0)
                return BadRequest();

            using SqlConnection connection =
                new SqlConnection(GetConnectionString());

            string query = @"
        UPDATE dbo.MaterialRequest
        SET Status = 'Rejected',
            AdminActions = 'Processed'
        WHERE Id = @Id
          AND (
              Status IN ('Pending', 'Pending Admin Approval')
              OR Status IS NULL
              OR LTRIM(RTRIM(Status)) = ''
          );";

            using SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await connection.OpenAsync();
            int rowsUpdated = await command.ExecuteNonQueryAsync();

            if (rowsUpdated != 1)
            {
                TempData["ErrorMessage"] =
                    "Request was not found or has already been processed.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                "Material request rejected successfully.";

            try
            {
                await SendDecisionEmailAsync(id, "Rejected");
                TempData["EmailSuccessMessage"] =
                    "Rejection notification email sent to Saibalaji_M@foxlink.com.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Request {RequestId} was rejected, but the rejection email failed.", id);

                TempData["EmailWarning"] =
                    "The request was rejected, but the email notification could not be sent.";
            }

            return RedirectToAction(nameof(Index));
        }


        private async Task SendDecisionEmailAsync(int id, string decision)
        {
            // Get the latest saved request details from the database.
            string reqId;
            DateTime requestDate;
            string projectCode;
            string requestedBy;
            string spareDescription;
            string partNumber;
            string brand;
            string targetMachine;
            int requestedQuantity;
            string priorityLevel;
            string justification;

            using (SqlConnection connection =
                new SqlConnection(GetConnectionString()))
            {
                const string query = @"
            SELECT
                ReqId,
                RequestDate,
                ProjectCode,
                RequestedBy,
                SpareDescription,
                PartNumber,
                Brand,
                TargetMachine,
                RequestedQuantity,
                PriorityLevel,
                Justification
            FROM dbo.MaterialRequest
            WHERE Id = @Id;";

                using SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                await connection.OpenAsync();

                using SqlDataReader reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException(
                        $"Material request {id} could not be found for email notification.");
                }

                reqId = Convert.ToString(reader["ReqId"]) ?? id.ToString();
                requestDate = Convert.ToDateTime(reader["RequestDate"]);
                projectCode = Convert.ToString(reader["ProjectCode"]) ?? "";
                requestedBy = Convert.ToString(reader["RequestedBy"]) ?? "";
                spareDescription = Convert.ToString(reader["SpareDescription"]) ?? "";
                partNumber = reader["PartNumber"] == DBNull.Value
                    ? "N/A" : Convert.ToString(reader["PartNumber"]) ?? "N/A";
                brand = reader["Brand"] == DBNull.Value
                    ? "N/A" : Convert.ToString(reader["Brand"]) ?? "N/A";
                targetMachine = Convert.ToString(reader["TargetMachine"]) ?? "";
                requestedQuantity = Convert.ToInt32(reader["RequestedQuantity"]);
                priorityLevel = Convert.ToString(reader["PriorityLevel"]) ?? "";
                justification = reader["Justification"] == DBNull.Value
                    ? "N/A" : Convert.ToString(reader["Justification"]) ?? "N/A";
            }

            // Use the existing SMTP settings from appsettings.json.
            var smtpSection = _configuration.GetSection("SmtpSettings");

            string host = smtpSection["Host"]
                ?? throw new InvalidOperationException("SMTP Host is missing.");

            int port = int.TryParse(smtpSection["Port"], out int smtpPort)
                ? smtpPort : 25;

            bool enableSsl = bool.TryParse(
                smtpSection["EnableSsl"], out bool ssl) && ssl;

            string senderEmail = smtpSection["SenderEmail"]
                ?? throw new InvalidOperationException("SMTP SenderEmail is missing.");

            string senderName = smtpSection["SenderName"]
                ?? "Testing Spare Management System";

            string username = smtpSection["Username"] ?? "";
            string password = smtpSection["Password"] ?? "";

            const string recipient = "Saibalaji_M@foxlink.com";

            string Safe(string? value) =>
                System.Net.WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(value) ? "N/A" : value);

            bool approved = string.Equals(
                decision, "Approved", StringComparison.OrdinalIgnoreCase);

            string subject =
                $"Material Request {decision} | {reqId} | {projectCode}";

            string heading = approved
                ? "Material Request Approved"
                : "Material Request Rejected";

            string messageText = approved
                ? "The material request listed below has been reviewed and approved by the administrator."
                : "The material request listed below has been reviewed and rejected by the administrator.";

            string statusColor = approved ? "#15803d" : "#b91c1c";
            string statusBackground = approved ? "#dcfce7" : "#fee2e2";

            string htmlBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin:0;padding:0;background:#f1f5f9;
                             font-family:Arial,Helvetica,sans-serif;color:#243247;'>
                  <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
                         style='background:#f1f5f9;padding:24px 10px;'>
                    <tr><td align='center'>
                      <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
                             style='max-width:680px;background:#ffffff;
                                    border:1px solid #dbe4ee;border-radius:10px;'>
                        <tr>
                          <td style='background:#123456;padding:25px 28px;color:#ffffff;'>
                            <div style='font-size:22px;font-weight:bold;'>
                              Testing Spare Management System
                            </div>
                            <div style='font-size:12px;letter-spacing:1px;
                                        color:#dbeafe;padding-top:8px;'>
                              MATERIAL REQUEST DECISION NOTIFICATION
                            </div>
                          </td>
                        </tr>
                        <tr>
                          <td style='padding:26px 28px 12px;'>
                            <span style='display:inline-block;background:{statusBackground};
                                         color:{statusColor};padding:8px 12px;
                                         border-radius:5px;font-size:13px;font-weight:bold;'>
                              {Safe(decision.ToUpperInvariant())}
                            </span>
                            <h2 style='margin:16px 0 8px;color:#123456;font-size:22px;'>
                              {Safe(heading)}
                            </h2>
                            <p style='font-size:14px;line-height:1.7;color:#526176;'>
                              Dear Balaji,<br><br>
                              {Safe(messageText)}
                              Please find the request details below for your records.
                            </p>
                          </td>
                        </tr>
                        <tr>
                          <td style='padding:10px 28px 28px;'>
                            <table role='presentation' width='100%' cellspacing='0'
                                   cellpadding='0'
                                   style='border-collapse:collapse;font-size:14px;'>
                              {EmailRow("Request ID", Safe(reqId))}
                              {EmailRow("Request Date", Safe(requestDate.ToString("dd MMM yyyy")))}
                              {EmailRow("Project Code", Safe(projectCode))}
                              {EmailRow("Requested By", Safe(requestedBy))}
                              {EmailRow("Spare Description", Safe(spareDescription))}
                              {EmailRow("Part Number", Safe(partNumber))}
                              {EmailRow("Brand / Manufacturer", Safe(brand))}
                              {EmailRow("Target Machine", Safe(targetMachine))}
                              {EmailRow("Requested Quantity", Safe(requestedQuantity.ToString("N0") + " Pcs/Sets"))}
                              {EmailRow("Priority", Safe(priorityLevel))}
                              {EmailRow("Decision", Safe(decision))}
                              {EmailRow("Justification", Safe(justification))}
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style='padding:18px 28px;background:#f8fafc;
                                     color:#64748b;font-size:12px;line-height:1.7;'>
                            This is an automated notification from the Testing Spare
                            Management System. Please do not reply directly to this email.
                          </td>
                        </tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>";

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            message.To.Add(recipient);

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(username, password);
            }

            await smtp.SendMailAsync(message);
        }

        private static string EmailRow(string label, string value)
        {
            return $@"
        <tr>
            <td style='width:38%;padding:11px;background:#f8fafc;
                       border-bottom:1px solid #e2e8f0;color:#64748b;
                       vertical-align:top;'>
                {label}
            </td>
            <td style='padding:11px;border-bottom:1px solid #e2e8f0;
                       color:#243247;'>
                {value}
            </td>
        </tr>";
        }



        private async Task<(List<MaterialRequestItem> Records, int TotalRecords)>
            GetMaterialRequests(int pageNumber, int pageSize)
        {
            var list = new List<MaterialRequestItem>();
            int totalRecords = 0;

            string connectionString = GetConnectionString();

            using SqlConnection connection = new SqlConnection(connectionString);

            using SqlCommand command = new SqlCommand("dbo.GetMaterialRequestReport", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            SqlParameter totalParam = command.Parameters.Add("@TotalRecords", SqlDbType.Int);
            totalParam.Direction = ParameterDirection.Output;

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(MapMaterialRequest(reader));
            }

            await reader.CloseAsync();

            if (totalParam.Value != DBNull.Value)
            {
                totalRecords = Convert.ToInt32(totalParam.Value);
            }

            return (list, totalRecords);
        }


        private MaterialRequestItem MapMaterialRequest(SqlDataReader reader)
        {
            return new MaterialRequestItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ReqId = reader["ReqId"]?.ToString() ?? "",
                RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                RequestedBy = reader["RequestedBy"]?.ToString() ?? "",
                SpareDescription = reader["SpareDescription"]?.ToString() ?? "",
                PartNumber = reader["PartNumber"] == DBNull.Value ? "" : reader["PartNumber"].ToString() ?? "",
                Brand = reader["Brand"] == DBNull.Value ? "" : reader["Brand"].ToString() ?? "",
                TargetMachine = reader["TargetMachine"]?.ToString() ?? "",
                RequestedQuantity = Convert.ToInt32(reader["RequestedQuantity"]),
                PriorityLevel = reader["PriorityLevel"]?.ToString() ?? "",
                Justification = reader["Justification"] == DBNull.Value ? "" : reader["Justification"].ToString() ?? "",
                Status = reader["Status"]?.ToString() ?? "",
                AdminActions = reader["AdminActions"]?.ToString() ?? "",
                IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"]),
                CreatedBy = reader["CreatedBy"] == DBNull.Value ? null : reader["CreatedBy"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"]),
                ModifiedBy = reader["ModifiedBy"] == DBNull.Value ? null : reader["ModifiedBy"].ToString(),
                ModifiedDate = reader["ModifiedDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["ModifiedDate"])
            };
        }


        private string GetConnectionString()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection was not found.");
            }

            return connectionString;
        }


        private async Task<List<SelectListItem>> GetActiveProjectCodes()
        {
            var projects = new List<SelectListItem>();

            using SqlConnection connection =
                new SqlConnection(GetConnectionString());

            string query = @"
        SELECT ProjectCode
        FROM dbo.ProjectMaster
        WHERE IsActive = 1
        ORDER BY ProjectCode;";

            using SqlCommand command =
                new SqlCommand(query, connection);

            await connection.OpenAsync();

            using SqlDataReader reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string projectCode =
                    reader["ProjectCode"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(projectCode))
                {
                    projects.Add(new SelectListItem
                    {
                        Value = projectCode,
                        Text = projectCode
                    });
                }
            }

            return projects;
        }


        private async Task SendMaterialRequestEmailAsync(
            MaterialRequestViewModel model,
            string requestedBy)
        {
            var smtpSection = _configuration.GetSection("SmtpSettings");

            string host = smtpSection["Host"]
                ?? throw new InvalidOperationException(
                    "SMTP Host is missing.");

            int port = int.TryParse(smtpSection["Port"], out int smtpPort)
                ? smtpPort
                : 25;

            bool enableSsl = bool.TryParse(
                smtpSection["EnableSsl"], out bool ssl)
                && ssl;

            string senderEmail = smtpSection["SenderEmail"]
                ?? throw new InvalidOperationException(
                    "SMTP SenderEmail is missing.");

            string senderName = smtpSection["SenderName"]
                ?? "SMT Production System";

            string username = smtpSection["Username"] ?? "";
            string password = smtpSection["Password"] ?? "";

            var recipients = smtpSection
                .GetSection("Recipients")
                .GetChildren()
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                throw new InvalidOperationException(
                    "No email recipients are configured.");
            }

            string Safe(string? value) =>
                WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(value) ? "N/A" : value);

            string requestDate = model.RequestDate.ToString("dd MMM yyyy");

            string subject =
                $"Material Request Submitted | {model.ProjectCode} | " +
                $"{model.SpareDescription}";

            string htmlBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin:0;padding:0;background:#f1f5f9;
                             font-family:Arial,Helvetica,sans-serif;color:#243247;'>

                <table role='presentation' width='100%' cellspacing='0'
                       cellpadding='0' border='0' style='background:#f1f5f9;'>
                <tr><td align='center' style='padding:24px 10px;'>

                <table role='presentation' width='100%' cellspacing='0'
                       cellpadding='0' border='0'
                       style='max-width:680px;background:#ffffff;
                              border:1px solid #dbe4ee;border-radius:10px;
                              overflow:hidden;'>

                <tr>
                <td style='background:#123456;padding:25px 28px;'>
                  <div style='font-size:23px;font-weight:bold;color:#ffffff;'>
                    Testing Spare Management System
                  </div>
                  <div style='font-size:12px;letter-spacing:1.5px;
                              color:#dbeafe;padding-top:8px;'>
                    MATERIAL REQUEST NOTIFICATION
                  </div>
                </td>
                </tr>

                <tr>
                <td style='padding:26px 28px 12px;'>
                  <div style='display:inline-block;background:#e0f2fe;
                              color:#075985;padding:7px 12px;border-radius:4px;
                              font-size:12px;font-weight:bold;'>
                    NEW REQUEST SUBMITTED
                  </div>

                  <h2 style='margin:16px 0 8px;font-size:22px;color:#123456;'>
                    Material Request Notification
                  </h2>

                  <p style='margin:0;line-height:1.6;color:#526176;font-size:14px;'>
                    A new material request has been successfully submitted
                    in the Testing Store Management System.
                  </p>
                </td>
                </tr>

                <tr>
                <td style='padding:12px 28px 8px;'>
                  <h3 style='margin:0 0 12px;font-size:16px;color:#123456;'>
                    Request Details
                  </h3>

                  <table role='presentation' width='100%' cellspacing='0'
                         cellpadding='0' border='0'
                         style='font-size:14px;border-collapse:collapse;'>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;width:38%;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Request Date
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;
                                 font-weight:bold;'>
                        {Safe(requestDate)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Project
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;
                                 font-weight:bold;'>
                        {Safe(model.ProjectCode)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Requested By
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;'>
                        {Safe(requestedBy)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Spare Description
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;
                                 font-weight:bold;'>
                        {Safe(model.SpareDescription)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Part Number
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;'>
                        {Safe(model.PartNumber)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Brand
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;'>
                        {Safe(model.Brand)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Target Machine
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;'>
                        {Safe(model.TargetMachine)}
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Requested Quantity
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;
                                 font-size:17px;font-weight:bold;color:#0875c9;'>
                        {model.RequestedQuantity:N0} Pcs
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 border-bottom:1px solid #e2e8f0;color:#64748b;'>
                        Priority Level
                      </td>
                      <td style='padding:11px;border-bottom:1px solid #e2e8f0;'>
                        <span style='display:inline-block;background:#eaf2ff;
                                     color:#1d4ed8;padding:5px 9px;
                                     border-radius:4px;font-weight:bold;'>
                          {Safe(model.PriorityLevel)}
                        </span>
                      </td>
                    </tr>

                    <tr>
                      <td style='padding:11px;background:#f8fafc;
                                 color:#64748b;vertical-align:top;'>
                        Justification
                      </td>
                      <td style='padding:11px;line-height:1.6;'>
                        {Safe(model.Justification)}
                      </td>
                    </tr>

                  </table>
                </td>
                </tr>

                <tr>
                <td style='padding:24px 28px 28px;'>
                  <div style='background:#f8fafc;border-left:4px solid #1685cf;
                              padding:14px 16px;font-size:13px;line-height:1.7;
                              color:#526176;'>
                    <strong style='color:#243247;'>Next Step</strong><br>
                    Please review the material request in the application
                    and take the necessary action according to the approval workflow.
                  </div>

                  <p style='font-size:12px;line-height:1.7;color:#94a3b8;
                            margin:20px 0 0;text-align:center;'>
                    This is an automated email from the Testing Store Management System.
                    Please do not reply directly to this notification.
                  </p>

                  <p style='font-size:12px;color:#64748b;text-align:center;
                            margin:8px 0 0;'>
                    Testing Spare Management System
                  </p>
                </td>
                </tr>

                </table>
                </td></tr>
                </table>
                </body>
                </html>";

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            foreach (string recipient in recipients)
            {
                message.To.Add(recipient!);
            }

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(username, password);
            }

            await smtp.SendMailAsync(message);
        }

        private async Task<List<string>> GetTestingStations()
        {
            var stations = new List<string>();

            using SqlConnection connection =
                new SqlConnection(GetConnectionString());

            const string query = @"
        SELECT StationCode
        FROM dbo.TestingStation
        WHERE IsActive = 1
        ORDER BY StationCode;";

            using SqlCommand command = new SqlCommand(query, connection);

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string station = reader["StationCode"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(station))
                {
                    stations.Add(station);
                }
            }

            return stations;
        }
    }
}