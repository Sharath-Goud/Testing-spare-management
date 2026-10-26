using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Net.Mail;

namespace TestingSpareManagement.Services
{
    public sealed class LowStockEmailBackgroundService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LowStockEmailBackgroundService> _logger;

        private static readonly TimeSpan CheckInterval =
            TimeSpan.FromMinutes(5);

        public LowStockEmailBackgroundService(
            IConfiguration configuration,
            ILogger<LowStockEmailBackgroundService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Low-stock email background service started.");

            // Check immediately when the service starts,
            // then repeat at the configured interval.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckLowStockAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected error while checking low stock.");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Low-stock email background service stopped.");
        }

        private async Task CheckLowStockAsync(
            CancellationToken cancellationToken)
        {
            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is missing.");

            using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            const string selectSql = @"
                SELECT
                    e.Id,
                    e.ProjectCode,
                    e.TestItemName,
                    e.AssignedEquipment,
                    e.StorageLocation,
                    e.AvailableQuantity,
                    e.AlertThreshold,
                    s.AlertSent
                FROM dbo.EquipmentConsumable AS e
                LEFT JOIN dbo.EquipmentLowStockAlertState AS s
                    ON s.EquipmentId = e.Id
                WHERE e.IsActive = 1
                ORDER BY e.Id;";

            var equipmentItems = new List<LowStockItem>();

            using (var command =
                new SqlCommand(selectSql, connection))
            {
                using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    equipmentItems.Add(new LowStockItem
                    {
                        Id = reader.GetInt32(0),
                        ProjectCode = reader.IsDBNull(1)
                            ? string.Empty : reader.GetString(1),
                        TestItemName = reader.IsDBNull(2)
                            ? string.Empty : reader.GetString(2),
                        AssignedEquipment = reader.IsDBNull(3)
                            ? string.Empty : reader.GetString(3),
                        StorageLocation = reader.IsDBNull(4)
                            ? string.Empty : reader.GetString(4),
                        AvailableQuantity = reader.GetInt32(5),
                        AlertThreshold = reader.GetInt32(6),
                        AlertSent = !reader.IsDBNull(7)
                            && reader.GetBoolean(7)
                    });
                }
            }

            foreach (var item in equipmentItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool isLowStock =
                    item.AvailableQuantity <= item.AlertThreshold;

                if (!isLowStock)
                {
                    // Stock recovered: allow a new alert
                    // if the item becomes low again later.
                    await ResetAlertStateAsync(
                        connection,
                        item.Id,
                        cancellationToken);

                    continue;
                }

                if (item.AlertSent)
                {
                    // An alert has already been sent for this
                    // low-stock episode.
                    continue;
                }

                bool sent = await SendLowStockEmailAsync(
                    item,
                    cancellationToken);

                if (sent)
                {
                    await MarkAlertSentAsync(
                        connection,
                        item.Id,
                        cancellationToken);
                }
                else
                {
                    // Leave AlertSent = 0. The next check
                    // will retry sending this notification.
                    _logger.LogWarning(
                        "Low-stock email for equipment ID {EquipmentId} " +
                        "was not sent. It will be retried.",
                        item.Id);
                }
            }
        }

        private async Task<bool> SendLowStockEmailAsync(
            LowStockItem item,
            CancellationToken cancellationToken)
        {
            string host =
                _configuration["SmtpSettings:Host"] ?? string.Empty;

            string senderEmail =
                _configuration["SmtpSettings:SenderEmail"]
                ?? string.Empty;

            string senderName =
                _configuration["SmtpSettings:SenderName"]
                ?? "Testing Spare Management System";

            int port = _configuration.GetValue<int>(
                "SmtpSettings:Port", 25);

            bool enableSsl = _configuration.GetValue<bool>(
                "SmtpSettings:EnableSsl", false);

            string username =
                _configuration["SmtpSettings:Username"] ?? string.Empty;

            string password =
                _configuration["SmtpSettings:Password"] ?? string.Empty;

            string[] recipients =
                _configuration
                    .GetSection("SmtpSettings:Recipients")
                    .Get<string[]>() ?? Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                port <= 0 ||
                recipients.Length == 0)
            {
                _logger.LogError(
                    "SMTP configuration is incomplete. " +
                    "Check SmtpSettings in appsettings.json.");

                return false;
            }

            try
            {
                string applicationUrl =
    _configuration["ApplicationUrl"]?.TrimEnd('/')
    ?? string.Empty;

                string equipmentUrl = string.IsNullOrWhiteSpace(applicationUrl)
                    ? string.Empty
                    : $"{applicationUrl}/Equipment/Index";

                string today = DateTime.Today.ToString("dd MMM yyyy");

                string encodedProject =
                    WebUtility.HtmlEncode(item.ProjectCode);

                string encodedItem =
                    WebUtility.HtmlEncode(item.TestItemName);

                string encodedEquipment =
                    WebUtility.HtmlEncode(item.AssignedEquipment);

                string encodedLocation =
                    WebUtility.HtmlEncode(item.StorageLocation);

                string buttonHtml = string.IsNullOrWhiteSpace(equipmentUrl)
                    ? string.Empty
                    : $@"
        <a href=""{WebUtility.HtmlEncode(equipmentUrl)}""
           style=""display:inline-block;
                  background-color:#bd1e1e;
                  color:#ffffff;
                  padding:15px 24px;
                  text-decoration:none;
                  border-radius:5px;
                  font-size:16px;
                  font-weight:bold;"">
            Open Equipment Consumables
        </a>";

                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),

                    Subject = $"LOW STOCK ALERT - {item.TestItemName}",

                    IsBodyHtml = true,

                    Body = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <meta charset=""UTF-8"">
                            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                            <title>Low Stock Alert</title>
                        </head>

                        <body style=""margin:0;
                                     padding:0;
                                     background-color:#ffffff;
                                     font-family:Arial,Helvetica,sans-serif;
                                     color:#333333;"">

                            <table role=""presentation""
                                   width=""100%""
                                   cellpadding=""0""
                                   cellspacing=""0""
                                   border=""0""
                                   style=""background-color:#ffffff;"">

                                <tr>
                                    <td align=""center"" style=""padding:15px;"">

                                        <table role=""presentation""
                                               width=""100%""
                                               cellpadding=""0""
                                               cellspacing=""0""
                                               border=""0""
                                               style=""max-width:850px;
                                                      border:1px solid #dddddd;
                                                      border-radius:9px;
                                                      overflow:hidden;"">

                                            <!-- RED HEADER -->
                                            <tr>
                                                <td style=""background-color:#bd1e1e;
                                                           padding:25px 22px;"">

                                                    <h1 style=""margin:0;
                                                               color:#ffffff;
                                                               font-size:30px;
                                                               font-weight:bold;"">
                                                        Low Stock Alert
                                                    </h1>

                                                </td>
                                            </tr>

                                            <!-- EMAIL CONTENT -->
                                            <tr>
                                                <td style=""padding:28px;"">

                                                    <p style=""font-size:18px;
                                                              line-height:1.6;
                                                              margin:0 0 20px 0;"">
                                                        Dear Team,
                                                    </p>

                                                    <p style=""font-size:18px;
                                                              line-height:1.6;
                                                              margin:0 0 20px 0;"">
                                                        This is an automated notification from the
                                                        Testing Spare Management System.
                                                    </p>

                                                    <p style=""font-size:18px;
                                                              line-height:1.6;
                                                              margin:0 0 20px 0;"">
                                                        The system has detected that the available
                                                        stock of an equipment consumable has reached
                                                        or fallen below its configured alert threshold.
                                                        Please review the details below and arrange
                                                        replenishment if required.
                                                    </p>

                                                    <!-- DATE -->
                                                    <p style=""font-size:18px;
                                                              margin:25px 0;"">
                                                        <strong>Date:</strong> {today}
                                                    </p>

                                                    <!-- ALERT DETAILS -->
                                                    <table role=""presentation""
                                                           width=""100%""
                                                           cellpadding=""0""
                                                           cellspacing=""0""
                                                           border=""0""
                                                           style=""background-color:#fff7ed;
                                                                  border-left:5px solid #f4511e;
                                                                  margin:20px 0 25px 0;"">

                                                        <tr>
                                                            <td style=""padding:20px;"">

                                                                <h3 style=""margin:0 0 18px 0;
                                                                           font-size:20px;
                                                                           color:#333333;"">
                                                                    Alert Details
                                                                </h3>

                                                                <p style=""font-size:17px;
                                                                          line-height:1.7;
                                                                          margin:0;"">
                                                                    &#8226; Low stock detected for
                                                                    <strong>{encodedItem}</strong>.
                                                                    Immediate stock review is recommended.
                                                                </p>

                                                            </td>
                                                        </tr>
                                                    </table>

                                                    <!-- EQUIPMENT DETAILS -->
                                                    <h3 style=""font-size:20px;
                                                               margin:25px 0 15px 0;
                                                               color:#333333;"">
                                                        Equipment Details
                                                    </h3>

                                                    <table role=""presentation""
                                                           width=""100%""
                                                           cellpadding=""0""
                                                           cellspacing=""0""
                                                           border=""0""
                                                           style=""border-collapse:collapse;
                                                                  font-size:16px;"">

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;
                                                                       width:42%;"">
                                                                <strong>Equipment ID</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;"">
                                                                {item.Id}
                                                            </td>
                                                        </tr>

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;"">
                                                                <strong>Project Code</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;"">
                                                                {encodedProject}
                                                            </td>
                                                        </tr>

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;"">
                                                                <strong>Spare Item</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;"">
                                                                {encodedItem}
                                                            </td>
                                                        </tr>

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;"">
                                                                <strong>Assigned Equipment / Station</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;"">
                                                                {encodedEquipment}
                                                            </td>
                                                        </tr>

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;"">
                                                                <strong>Storage Location</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;"">
                                                                {encodedLocation}
                                                            </td>
                                                        </tr>

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;"">
                                                                <strong>Available Quantity</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       color:#bd1e1e;
                                                                       font-weight:bold;
                                                                       font-size:18px;"">
                                                                {item.AvailableQuantity}
                                                            </td>
                                                        </tr>

                                                        <tr>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;
                                                                       background-color:#f5f5f5;"">
                                                                <strong>Alert Threshold</strong>
                                                            </td>
                                                            <td style=""padding:12px;
                                                                       border:1px solid #dddddd;"">
                                                                {item.AlertThreshold}
                                                            </td>
                                                        </tr>

                                                    </table>

                                                    <!-- ACTION BUTTON -->
                                                    <table role=""presentation""
                                                           cellpadding=""0""
                                                           cellspacing=""0""
                                                           border=""0""
                                                           style=""margin:25px 0;"">
                                                        <tr>
                                                            <td>
                                                                {buttonHtml}
                                                            </td>
                                                        </tr>
                                                    </table>

                                                    <p style=""font-size:18px;
                                                              line-height:1.6;
                                                              margin:20px 0;"">
                                                        Please take the necessary action at the earliest
                                                        to avoid any disruption caused by insufficient stock.
                                                    </p>

                                                    <p style=""font-size:18px;
                                                              line-height:1.6;
                                                              margin:25px 0 0 0;"">
                                                        Regards,<br>
                                                        <strong>Testing Spare Management System</strong>
                                                    </p>

                                                </td>
                                            </tr>

                                        </table>

                                    </td>
                                </tr>

                            </table>

                        </body>
                        </html>"
                };

                foreach (string recipient in recipients)
                {
                    if (!string.IsNullOrWhiteSpace(recipient))
                    {
                        message.To.Add(
                            new MailAddress(recipient.Trim()));
                    }
                }

                if (message.To.Count == 0)
                {
                    _logger.LogError(
                        "No valid recipients are configured.");
                    return false;
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
                    smtp.Credentials =
                        new NetworkCredential(username, password);
                }
                else
                {
                    // Use this only if your SMTP relay allows
                    // unauthenticated sending from this server.
                    smtp.UseDefaultCredentials = false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                await smtp.SendMailAsync(message);

                _logger.LogInformation(
                    "Low-stock email accepted by SMTP for equipment ID {Id}.",
                    item.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send low-stock email for equipment ID {Id}.",
                    item.Id);

                return false;
            }
        }

        private static async Task MarkAlertSentAsync(
            SqlConnection connection,
            int equipmentId,
            CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE dbo.EquipmentLowStockAlertState
                SET AlertSent = 1,
                    LastAlertSentAt = GETDATE(),
                    ModifiedDate = GETDATE()
                WHERE EquipmentId = @EquipmentId;

                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO dbo.EquipmentLowStockAlertState
                    (
                        EquipmentId,
                        AlertSent,
                        LastAlertSentAt,
                        ModifiedDate
                    )
                    VALUES
                    (
                        @EquipmentId,
                        1,
                        GETDATE(),
                        GETDATE()
                    );
                END;";

            using var command = new SqlCommand(sql, connection);

            command.Parameters.Add(
                "@EquipmentId", SqlDbType.Int).Value = equipmentId;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task ResetAlertStateAsync(
            SqlConnection connection,
            int equipmentId,
            CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE dbo.EquipmentLowStockAlertState
                SET AlertSent = 0,
                    ModifiedDate = GETDATE()
                WHERE EquipmentId = @EquipmentId;";

            using var command = new SqlCommand(sql, connection);

            command.Parameters.Add(
                "@EquipmentId", SqlDbType.Int).Value = equipmentId;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private sealed class LowStockItem
        {
            public int Id { get; set; }
            public string ProjectCode { get; set; } = string.Empty;
            public string TestItemName { get; set; } = string.Empty;
            public string AssignedEquipment { get; set; } = string.Empty;
            public string StorageLocation { get; set; } = string.Empty;
            public int AvailableQuantity { get; set; }
            public int AlertThreshold { get; set; }
            public bool AlertSent { get; set; }
        }
    }
}