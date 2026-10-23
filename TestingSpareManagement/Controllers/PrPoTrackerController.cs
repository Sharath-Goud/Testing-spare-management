using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using TestingSpareManagement.Models;

using SqlDbType = System.Data.SqlDbType;
using CommandType = System.Data.CommandType;
using ParameterDirection = System.Data.ParameterDirection;

namespace TestingSpareManagement.Controllers
{
    public class PrPoTrackerController : Controller
    {
        private readonly IConfiguration _configuration;

        public PrPoTrackerController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;

            var model = new PrPoTrackerViewModel
            {
                ActiveYear = activeYear,
                RequisitionDate = DateTime.Today
            };

            model.Projects = await GetProjectsFromDb();
            model.Buyers = await GetBuyersFromDb();
            model.Origins = await GetOriginsFromDb();
            model.PoStatuses = await GetPoStatusesFromDb();
            model.ShipmentStatuses = await GetShipmentStatusesFromDb();
            ViewBag.AssetTargets = await GetMasterConnectorDescriptions();
            var (records, _) = await GetRecords(activeYear, 1, 500);
            model.Records = records;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(PrPoTrackerViewModel model)
        {
            model.ProjectCode = (model.ProjectCode ?? "").Trim();
            model.PrNumber = (model.PrNumber ?? "").Trim();
            model.PoStatus = (model.PoStatus ?? "").Trim();
            model.PurchaseOriginRegion = (model.PurchaseOriginRegion ?? "").Trim();
            model.BuyerName = (model.BuyerName ?? "").Trim();
            model.AssetTarget = (model.AssetTarget ?? "").Trim();
            model.ShipmentTrackingNo = model.ShipmentTrackingNo?.Trim();
            model.ShipmentStatus = model.ShipmentStatus?.Trim();
            model.Remarks = model.Remarks?.Trim();

            if (!ModelState.IsValid)
            {
                model.Projects = await GetProjectsFromDb();
                model.Buyers = await GetBuyersFromDb();
                model.Origins = await GetOriginsFromDb();
                model.PoStatuses = await GetPoStatusesFromDb();
                model.ShipmentStatuses = await GetShipmentStatusesFromDb();
                ViewBag.AssetTargets = await GetMasterConnectorDescriptions();
                var (records, _) = await GetRecords(model.ActiveYear, 1, 500);
                model.Records = records;

                return View("Index", model);
            }

            try
            {
                string conn = GetConnectionString();
                string createdBy = HttpContext.Session.GetString("EmployeeName") ?? "System";

                using SqlConnection connection = new SqlConnection(conn);
                await connection.OpenAsync();

                await SaveMasterDataIfNew(
                    connection,
                    model.ProjectCode ?? "",
                    model.BuyerName ?? "",
                    model.PurchaseOriginRegion ?? "",
                    model.PoStatus ?? "",
                    model.ShipmentStatus,
                    createdBy);

                using SqlCommand command = new SqlCommand("dbo.InsertPrPo", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@RequisitionDate", SqlDbType.Date).Value = model.RequisitionDate;
                command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = model.ProjectCode ?? "";
                command.Parameters.Add("@PrNumber", SqlDbType.NVarChar, 50).Value = model.PrNumber ?? "";
                command.Parameters.Add("@PoStatus", SqlDbType.NVarChar, 50).Value = model.PoStatus ?? "";
                command.Parameters.Add("@PurchaseOriginRegion", SqlDbType.NVarChar, 50).Value = model.PurchaseOriginRegion ?? "";
                command.Parameters.Add("@BuyerName", SqlDbType.NVarChar, 100).Value = model.BuyerName ?? "";
                command.Parameters.Add("@AssetTarget", SqlDbType.NVarChar, 100).Value = model.AssetTarget ?? "";
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = model.Quantity;

                var priceParam = command.Parameters.Add("@UnitPrice", SqlDbType.Decimal);
                priceParam.Precision = 18;
                priceParam.Scale = 2;
                priceParam.Value = model.UnitPrice;

                var taxParam = command.Parameters.Add("@TaxPercent", SqlDbType.Decimal);
                taxParam.Precision = 5;
                taxParam.Scale = 2;
                taxParam.Value = model.TaxPercent;

                command.Parameters.Add("@ShipmentTrackingNo", SqlDbType.NVarChar, 100).Value =
                    string.IsNullOrWhiteSpace(model.ShipmentTrackingNo) ? DBNull.Value : (object)model.ShipmentTrackingNo!;

                command.Parameters.Add("@DeliveryDate", SqlDbType.Date).Value =
                    model.DeliveryDate.HasValue ? (object)model.DeliveryDate.Value : DBNull.Value;

                command.Parameters.Add("@ShipmentStatus", SqlDbType.NVarChar, 50).Value =
                    string.IsNullOrWhiteSpace(model.ShipmentStatus) ? DBNull.Value : (object)model.ShipmentStatus!;

                command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(model.Remarks) ? DBNull.Value : (object)model.Remarks!;

                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;

                await command.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = $"PR/PO record saved successfully for {model.ProjectCode}!";
                return RedirectToAction(nameof(Index), new { year = model.ActiveYear });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to save: {ex.Message}";

                model.Projects = await GetProjectsFromDb();
                model.Buyers = await GetBuyersFromDb();
                model.Origins = await GetOriginsFromDb();
                model.PoStatuses = await GetPoStatusesFromDb();
                model.ShipmentStatuses = await GetShipmentStatusesFromDb();

                var (records, _) = await GetRecords(model.ActiveYear, 1, 500);
                model.Records = records;

                return View("Index", model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetEditRecord(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid record ID." });

            try
            {
                using SqlConnection connection =
                    new SqlConnection(GetConnectionString());

                const string query = @"
            SELECT
                Id,
                RequisitionDate,
                ProjectCode,
                PrNumber,
                PoStatus,
                PurchaseOriginRegion,
                BuyerName,
                AssetTarget,
                Quantity,
                UnitPrice,
                TaxPercent,
                ShipmentTrackingNo,
                DeliveryDate,
                ShipmentStatus,
                Remarks
            FROM dbo.PrPoTracker
            WHERE Id = @Id AND IsActive = 1;";

                using SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                await connection.OpenAsync();

                using SqlDataReader reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "PR/PO record not found."
                    });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = Convert.ToInt32(reader["Id"]),
                        requisitionDate = Convert.ToDateTime(reader["RequisitionDate"])
                            .ToString("yyyy-MM-dd"),
                        projectCode = reader["ProjectCode"]?.ToString() ?? "",
                        prNumber = reader["PrNumber"]?.ToString() ?? "",
                        poStatus = reader["PoStatus"]?.ToString() ?? "",
                        purchaseOriginRegion =
                            reader["PurchaseOriginRegion"]?.ToString() ?? "",
                        buyerName = reader["BuyerName"]?.ToString() ?? "",
                        assetTarget = reader["AssetTarget"]?.ToString() ?? "",
                        quantity = Convert.ToInt32(reader["Quantity"]),
                        unitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                        taxPercent = Convert.ToDecimal(reader["TaxPercent"]),
                        shipmentTrackingNo =
                            reader["ShipmentTrackingNo"] == DBNull.Value
                                ? "" : reader["ShipmentTrackingNo"].ToString(),
                        deliveryDate =
                            reader["DeliveryDate"] == DBNull.Value
                                ? "" : Convert.ToDateTime(reader["DeliveryDate"])
                                    .ToString("yyyy-MM-dd"),
                        shipmentStatus =
                            reader["ShipmentStatus"] == DBNull.Value
                                ? "" : reader["ShipmentStatus"].ToString(),
                        remarks =
                            reader["Remarks"] == DBNull.Value
                                ? "" : reader["Remarks"].ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                // Replace with your existing ILogger if one is injected.
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to load the PR/PO record.",
                    detail = ex.Message
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int id,
            PrPoTrackerViewModel model)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid PR/PO record ID.";
                return RedirectToAction(nameof(Index), new { year = model.ActiveYear });
            }

            if (model.Quantity <= 0 ||
                model.UnitPrice < 0 ||
                string.IsNullOrWhiteSpace(model.ProjectCode) ||
                string.IsNullOrWhiteSpace(model.PrNumber) ||
                string.IsNullOrWhiteSpace(model.PoStatus) ||
                string.IsNullOrWhiteSpace(model.PurchaseOriginRegion) ||
                string.IsNullOrWhiteSpace(model.BuyerName) ||
                string.IsNullOrWhiteSpace(model.AssetTarget))
            {
                TempData["ErrorMessage"] =
                    "Please provide all required PR/PO fields with valid values.";

                return RedirectToAction(nameof(Index), new { year = model.ActiveYear });
            }

            bool delivered = string.Equals(
                model.ShipmentStatus?.Trim(),
                "Delivered to floor",
                StringComparison.OrdinalIgnoreCase);

            try
            {
                using SqlConnection connection =
                    new SqlConnection(GetConnectionString());

                await connection.OpenAsync();

                using SqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    // Read existing inventory contribution and lock the PR/PO row.
                    int appliedQuantity;

                    const string readQuery = @"
                SELECT InventoryAppliedQuantity
                FROM dbo.PrPoTracker WITH (UPDLOCK, HOLDLOCK)
                WHERE Id = @Id AND IsActive = 1;";

                    using (SqlCommand command =
                        new SqlCommand(readQuery, connection, transaction))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                        object result = await command.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                            throw new InvalidOperationException(
                                "PR/PO record was not found or is inactive.");

                        appliedQuantity = Convert.ToInt32(result);
                    }

                    // Update the PR/PO record.
                    decimal subtotal = model.Quantity * model.UnitPrice;
                    decimal taxAmount = subtotal * model.TaxPercent / 100m;
                    decimal totalCost = subtotal + taxAmount;

                    const string updateQuery = @"
                UPDATE dbo.PrPoTracker
                SET RequisitionDate = @RequisitionDate,
                    ProjectCode = @ProjectCode,
                    PrNumber = @PrNumber,
                    PoStatus = @PoStatus,
                    PurchaseOriginRegion = @PurchaseOriginRegion,
                    BuyerName = @BuyerName,
                    AssetTarget = @AssetTarget,
                    Quantity = @Quantity,
                    UnitPrice = @UnitPrice,
                    Subtotal = @Subtotal,
                    TaxPercent = @TaxPercent,
                    TaxAmount = @TaxAmount,
                    TotalCost = @TotalCost,
                    ShipmentTrackingNo = @ShipmentTrackingNo,
                    DeliveryDate = @DeliveryDate,
                    ShipmentStatus = @ShipmentStatus,
                    Remarks = @Remarks,
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = GETDATE()
                WHERE Id = @Id AND IsActive = 1;";

                    using (SqlCommand command =
                        new SqlCommand(updateQuery, connection, transaction))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                        command.Parameters.Add("@RequisitionDate", SqlDbType.Date)
                            .Value = model.RequisitionDate.Date;
                        command.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50)
                            .Value = model.ProjectCode.Trim();
                        command.Parameters.Add("@PrNumber", SqlDbType.NVarChar, 50)
                            .Value = model.PrNumber.Trim();
                        command.Parameters.Add("@PoStatus", SqlDbType.NVarChar, 50)
                            .Value = model.PoStatus.Trim();
                        command.Parameters.Add("@PurchaseOriginRegion", SqlDbType.NVarChar, 50)
                            .Value = model.PurchaseOriginRegion.Trim();
                        command.Parameters.Add("@BuyerName", SqlDbType.NVarChar, 100)
                            .Value = model.BuyerName.Trim();
                        command.Parameters.Add("@AssetTarget", SqlDbType.NVarChar, 250)
                            .Value = model.AssetTarget.Trim();
                        command.Parameters.Add("@Quantity", SqlDbType.Int)
                            .Value = model.Quantity;

                        var price = command.Parameters.Add("@UnitPrice", SqlDbType.Decimal);
                        price.Precision = 18;
                        price.Scale = 2;
                        price.Value = model.UnitPrice;

                        var sub = command.Parameters.Add("@Subtotal", SqlDbType.Decimal);
                        sub.Precision = 18;
                        sub.Scale = 2;
                        sub.Value = subtotal;

                        var tax = command.Parameters.Add("@TaxPercent", SqlDbType.Decimal);
                        tax.Precision = 5;
                        tax.Scale = 2;
                        tax.Value = model.TaxPercent;

                        var taxAmt = command.Parameters.Add("@TaxAmount", SqlDbType.Decimal);
                        taxAmt.Precision = 18;
                        taxAmt.Scale = 2;
                        taxAmt.Value = taxAmount;

                        var total = command.Parameters.Add("@TotalCost", SqlDbType.Decimal);
                        total.Precision = 18;
                        total.Scale = 2;
                        total.Value = totalCost;

                        command.Parameters.Add("@ShipmentTrackingNo", SqlDbType.NVarChar, 100)
                            .Value = string.IsNullOrWhiteSpace(model.ShipmentTrackingNo)
                                ? DBNull.Value : (object)model.ShipmentTrackingNo.Trim();

                        command.Parameters.Add("@DeliveryDate", SqlDbType.Date)
                            .Value = model.DeliveryDate.HasValue
                                ? (object)model.DeliveryDate.Value.Date : DBNull.Value;

                        command.Parameters.Add("@ShipmentStatus", SqlDbType.NVarChar, 50)
                            .Value = string.IsNullOrWhiteSpace(model.ShipmentStatus)
                                ? DBNull.Value : (object)model.ShipmentStatus.Trim();

                        command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 500)
                            .Value = string.IsNullOrWhiteSpace(model.Remarks)
                                ? DBNull.Value : (object)model.Remarks.Trim();

                        command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100)
                            .Value = HttpContext.Session.GetString("EmployeeName") ?? "System";

                        if (await command.ExecuteNonQueryAsync() != 1)
                            throw new InvalidOperationException(
                                "PR/PO record could not be updated.");
                    }


                    string currencySymbol;

                    const string currencyQuery = @"
                        SELECT CurrencySymbol
                        FROM dbo.OriginMaster
                        WHERE LTRIM(RTRIM(OriginName)) = LTRIM(RTRIM(@OriginName))
                          AND IsActive = 1;";

                    using (SqlCommand currencyCommand =
                        new SqlCommand(currencyQuery, connection, transaction))
                    {
                        currencyCommand.Parameters.Add("@OriginName", SqlDbType.NVarChar, 50)
                            .Value = model.PurchaseOriginRegion.Trim();

                        object currencyResult = await currencyCommand.ExecuteScalarAsync();

                        if (currencyResult == null || currencyResult == DBNull.Value)
                        {
                            throw new InvalidOperationException(
                                "Currency was not found for the selected purchase origin.");
                        }

                        currencySymbol = currencyResult.ToString()?.Trim() ?? "";
                    }

                    bool isINR = currencySymbol == "₹" ||
                        currencySymbol.Equals("INR", StringComparison.OrdinalIgnoreCase);

                    bool isCNY = currencySymbol == "¥" ||
                        currencySymbol == "元" ||
                        currencySymbol.Equals("CNY", StringComparison.OrdinalIgnoreCase) ||
                        currencySymbol.Equals("RMB", StringComparison.OrdinalIgnoreCase);

                    if (!isINR && !isCNY)
                    {
                        throw new InvalidOperationException(
                            $"Unsupported purchase currency: {currencySymbol}");
                    }


                    // Add the quantity difference to inventory only when delivered.
                    if (delivered)
                    {

                        int difference = model.Quantity - appliedQuantity;
                        int affected = 0;

                        // First match: Master Connector.
                        const string connectorQuery = @"
                            UPDATE dbo.MasterConnector
                            SET BaseStock = BaseStock + @Difference,
                                UnitCostINR = CASE
                                    WHEN @IsINR = 1 THEN @UnitPrice
                                    ELSE UnitCostINR
                                END,
                                UnitCostCNY = CASE
                                    WHEN @IsCNY = 1 THEN @UnitPrice
                                    ELSE UnitCostCNY
                                END,
                                ModifiedDate = GETDATE()
                            WHERE ProjectCode = @ProjectCode
                              AND LTRIM(RTRIM(DescriptionPartNo)) =
                                  LTRIM(RTRIM(@AssetTarget))
                              AND IsActive = 1
                              AND BaseStock + @Difference >= 0;";

                        using (SqlCommand connectorCommand =
                            new SqlCommand(connectorQuery, connection, transaction))
                        {
                            connectorCommand.Parameters.Add("@Difference", SqlDbType.Int)
                                .Value = difference;

                            connectorCommand.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50)
                                .Value = model.ProjectCode.Trim();

                            connectorCommand.Parameters.Add("@AssetTarget", SqlDbType.NVarChar, 250)
                                .Value = model.AssetTarget.Trim();

                            var connectorPrice =
                                connectorCommand.Parameters.Add("@UnitPrice", SqlDbType.Decimal);
                            connectorPrice.Precision = 18;
                            connectorPrice.Scale = 2;
                            connectorPrice.Value = model.UnitPrice;

                            connectorCommand.Parameters.Add("@IsINR", SqlDbType.Bit)
                                .Value = isINR;

                            connectorCommand.Parameters.Add("@IsCNY", SqlDbType.Bit)
                                .Value = isCNY;

                            affected = await connectorCommand.ExecuteNonQueryAsync();
                        }

                        // If no connector matched, try Equipment Consumables.
                        if (affected == 0)
                        {
                            const string equipmentQuery = @"
                                UPDATE dbo.EquipmentConsumables
                                SET AvailableQty = AvailableQty + @Difference,
                                    UnitPriceINR = CASE
                                        WHEN @IsINR = 1 THEN @UnitPrice
                                        ELSE UnitPriceINR
                                    END,
                                    UnitPriceRMB = CASE
                                        WHEN @IsCNY = 1 THEN @UnitPrice
                                        ELSE UnitPriceRMB
                                    END,
                                    ModifiedDate = GETDATE()
                                WHERE ProjectCode = @ProjectCode
                                  AND LTRIM(RTRIM(TestItem)) = LTRIM(RTRIM(@AssetTarget))
                                  AND IsActive = 1
                                  AND AvailableQty + @Difference >= 0;";

                            using (SqlCommand equipmentCommand =
                                new SqlCommand(equipmentQuery, connection, transaction))
                            {
                                equipmentCommand.Parameters.Add("@Difference", SqlDbType.Int)
                                    .Value = difference;

                                equipmentCommand.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50)
                                    .Value = model.ProjectCode.Trim();

                                equipmentCommand.Parameters.Add("@AssetTarget", SqlDbType.NVarChar, 250)
                                    .Value = model.AssetTarget.Trim();

                                var equipmentPrice =
                                    equipmentCommand.Parameters.Add("@UnitPrice", SqlDbType.Decimal);
                                equipmentPrice.Precision = 18;
                                equipmentPrice.Scale = 2;
                                equipmentPrice.Value = model.UnitPrice;

                                equipmentCommand.Parameters.Add("@IsINR", SqlDbType.Bit)
                                    .Value = isINR;

                                affected = await equipmentCommand.ExecuteNonQueryAsync();
                            }
                        }

                        if (affected != 1)
                        {
                            throw new InvalidOperationException(
                                "No single active inventory item matched this project and spare, " +
                                "or the adjustment would make stock negative. No changes were saved.");
                        }


                        // Store the quantity already applied so it cannot be added twice.
                        const string appliedQuery = @"
                    UPDATE dbo.PrPoTracker
                    SET InventoryAppliedQuantity = @Quantity
                    WHERE Id = @Id;";

                        using SqlCommand command =
                            new SqlCommand(appliedQuery, connection, transaction);

                        command.Parameters.Add("@Quantity", SqlDbType.Int)
                            .Value = model.Quantity;
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = delivered
                        ? "PR/PO updated and delivered quantity reconciled with inventory."
                        : "PR/PO record updated successfully.";
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Update failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Index), new { year = model.ActiveYear });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int year)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid record ID.";
                return RedirectToAction(nameof(Index), new { year });
            }

            try
            {
                string conn = GetConnectionString();

                using SqlConnection connection = new SqlConnection(conn);
                using SqlCommand command = new SqlCommand("dbo.DeletePrPo", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 100).Value =
                    HttpContext.Session.GetString("EmployeeName") ?? "System";

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = "PR/PO record deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Delete failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;

            var (records, _) = await GetRecords(activeYear, 1, 10000);

            var sb = new StringBuilder();

            // CSV Header
            sb.AppendLine("DATE,PR NUMBER,PO STATUS,PROJECT,ORIGIN,BUYER,ASSET TARGET,QTY,UNIT PRICE,SUBTOTAL,TAX %,TAX AMOUNT,TOTAL COST,SHIPMENT STATUS,SHIPMENT NO,DELIVERY DATE,REMARKS");

            // CSV Rows
            foreach (var item in records)
            {
                sb.Append(Escape(item.RequisitionDate.ToString("yyyy-MM-dd"))).Append(',');
                sb.Append(Escape(item.PrNumber)).Append(',');
                sb.Append(Escape(item.PoStatus)).Append(',');
                sb.Append(Escape(item.ProjectCode)).Append(',');
                sb.Append(Escape(item.PurchaseOriginRegion)).Append(',');
                sb.Append(Escape(item.BuyerName)).Append(',');
                sb.Append(Escape(item.AssetTarget)).Append(',');
                sb.Append(item.Quantity).Append(',');
                sb.Append(item.UnitPrice.ToString("F2")).Append(',');
                sb.Append(item.Subtotal.ToString("F2")).Append(',');
                sb.Append(item.TaxPercent.ToString("F2")).Append(',');
                sb.Append(item.TaxAmount.ToString("F2")).Append(',');
                sb.Append(item.TotalCost.ToString("F2")).Append(',');
                sb.Append(Escape(item.ShipmentStatus ?? "")).Append(',');
                sb.Append(Escape(item.ShipmentTrackingNo ?? "")).Append(',');
                sb.Append(item.DeliveryDate?.ToString("yyyy-MM-dd") ?? "-").Append(',');
                sb.Append(Escape(item.Remarks ?? "")).AppendLine();
            }

            // BOM + UTF-8 for Excel
            string csvContent = "\uFEFF" + sb.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(csvContent);

            string fileName = $"PRPO_Tracker_{activeYear}_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int? year)
        {
            int activeYear = year ?? DateTime.Now.Year;

            var (records, _) = await GetRecords(activeYear, 1, 10000);

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine($"<title>PR & PO Tracker - {activeYear}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; padding: 20px; color: #333; }");
            sb.AppendLine("h2 { color: #6f42c1; margin-bottom: 8px; }");
            sb.AppendLine("p { font-size: 11px; color: #666; margin-bottom: 20px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; font-size: 9px; }");
            sb.AppendLine("th { background: #6f42c1; color: white; padding: 6px 4px; text-align: left; border: 1px solid #5a35a0; font-weight: 700; text-transform: uppercase; font-size: 8px; }");
            sb.AppendLine("td { padding: 6px 4px; border: 1px solid #ddd; }");
            sb.AppendLine("tr:nth-child(even) { background: #fafafa; }");
            sb.AppendLine("@media print { .no-print { display: none; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h2>Procurement Requisition & Purchase Tracking Ledger - {activeYear}</h2>");
            sb.AppendLine($"<p>Generated on: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>DATE</th>");
            sb.AppendLine("<th>PR NO</th>");
            sb.AppendLine("<th>PO STATUS</th>");
            sb.AppendLine("<th>PROJECT</th>");
            sb.AppendLine("<th>ORIGIN</th>");
            sb.AppendLine("<th>BUYER</th>");
            sb.AppendLine("<th>ASSET</th>");
            sb.AppendLine("<th>QTY</th>");
            sb.AppendLine("<th>UNIT PRICE</th>");
            sb.AppendLine("<th>TOTAL COST</th>");
            sb.AppendLine("<th>SHIPMENT STATUS</th>");
            sb.AppendLine("<th>SHIPMENT NO</th>");
            sb.AppendLine("<th>DELIVERY DATE</th>");
            sb.AppendLine("<th>REMARKS</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var item in records)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{item.RequisitionDate:yyyy-MM-dd}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.PrNumber)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.PoStatus)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.ProjectCode)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.PurchaseOriginRegion)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.BuyerName)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.AssetTarget)}</td>");
                sb.AppendLine($"<td>{item.Quantity}</td>");
                sb.AppendLine($"<td>${item.UnitPrice:F2}</td>");
                sb.AppendLine($"<td><strong>${item.TotalCost:F2}</strong></td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.ShipmentStatus ?? "")}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.ShipmentTrackingNo ?? "")}</td>");
                sb.AppendLine($"<td>{(item.DeliveryDate?.ToString("yyyy-MM-dd") ?? "-")}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Remarks ?? "")}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
            sb.AppendLine("<script>window.onload = function() { window.print(); };</" + "script>");
            sb.AppendLine("</body></html>");

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

            // Return as HTML (browser opens in new tab → auto print)
            return File(bytes, "text/html");
        }

        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Replace("\"", "\"\"");
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value + "\"";
            return value;
        }

        private async Task SaveMasterDataIfNew(
            SqlConnection connection,
            string projectCode,
            string buyerName,
            string originName,
            string poStatus,
            string? shipmentStatus,
            string createdBy)
        {
            if (!string.IsNullOrWhiteSpace(projectCode))
            {
                try
                {
                    using SqlCommand cmd = new SqlCommand("dbo.SaveProject", connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@ProjectCode", SqlDbType.NVarChar, 50).Value = projectCode;
                    cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 200).Value = DBNull.Value;
                    cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex) { Console.WriteLine($"SaveProject error: {ex.Message}"); }
            }

            if (!string.IsNullOrWhiteSpace(buyerName))
            {
                try
                {
                    using SqlCommand cmd = new SqlCommand("dbo.SaveBuyer", connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@BuyerName", SqlDbType.NVarChar, 100).Value = buyerName;
                    cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex) { Console.WriteLine($"SaveBuyer error: {ex.Message}"); }
            }

            if (!string.IsNullOrWhiteSpace(originName))
            {
                try
                {
                    using SqlCommand cmd = new SqlCommand("dbo.SaveOrigin", connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@OriginName", SqlDbType.NVarChar, 50).Value = originName;
                    cmd.Parameters.Add("@CountryCode", SqlDbType.NVarChar, 5).Value = DBNull.Value;
                    cmd.Parameters.Add("@TaxPercent", SqlDbType.Decimal).Value = 0m;
                    cmd.Parameters.Add("@CurrencySymbol", SqlDbType.NVarChar, 5).Value = "₹";
                    cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex) { Console.WriteLine($"SaveOrigin error: {ex.Message}"); }
            }

            if (!string.IsNullOrWhiteSpace(poStatus))
            {
                try
                {
                    using SqlCommand cmd = new SqlCommand("dbo.SavePoStatus", connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@StatusName", SqlDbType.NVarChar, 50).Value = poStatus;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex) { Console.WriteLine($"SavePoStatus error: {ex.Message}"); }
            }

            if (!string.IsNullOrWhiteSpace(shipmentStatus))
            {
                try
                {
                    using SqlCommand cmd = new SqlCommand("dbo.SaveShipmentStatus", connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@StatusName", SqlDbType.NVarChar, 50).Value = shipmentStatus!;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex) { Console.WriteLine($"SaveShipmentStatus error: {ex.Message}"); }
            }
        }

        private async Task<List<ProjectMasterModel>> GetProjectsFromDb()
        {
            var list = new List<ProjectMasterModel>();

            using SqlConnection connection = new SqlConnection(GetConnectionString());

            const string query = @"
                SELECT Id, ProjectCode
                FROM dbo.ProjectMaster
                WHERE IsActive = 1
                ORDER BY ProjectCode;";

            using SqlCommand command = new SqlCommand(query, connection);

            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ProjectMasterModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                    Description = null
                });
            }

            return list;
        }


        private async Task<List<BuyerMasterModel>> GetBuyersFromDb()
        {
            var list = new List<BuyerMasterModel>();
            try
            {
                using SqlConnection connection = new SqlConnection(GetConnectionString());
                using SqlCommand command = new SqlCommand("dbo.GetActiveBuyers", connection);
                command.CommandType = CommandType.StoredProcedure;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new BuyerMasterModel
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        BuyerName = reader["BuyerName"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error loading buyers: {ex.Message}"); }
            return list;
        }

        private async Task<List<OriginMasterModel>> GetOriginsFromDb()
        {
            var list = new List<OriginMasterModel>();
            try
            {
                using SqlConnection connection = new SqlConnection(GetConnectionString());
                using SqlCommand command = new SqlCommand("dbo.GetActiveOrigins", connection);
                command.CommandType = CommandType.StoredProcedure;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new OriginMasterModel
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        OriginName = reader["OriginName"]?.ToString() ?? "",
                        CountryCode = reader["CountryCode"] == DBNull.Value ? "" : reader["CountryCode"].ToString() ?? "",
                        TaxPercent = reader["TaxPercent"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TaxPercent"]),
                        CurrencySymbol = reader["CurrencySymbol"] == DBNull.Value ? "₹" : reader["CurrencySymbol"].ToString() ?? "₹"
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error loading origins: {ex.Message}"); }
            return list;
        }

        private async Task<List<PoStatusMasterModel>> GetPoStatusesFromDb()
        {
            var list = new List<PoStatusMasterModel>();
            try
            {
                using SqlConnection connection = new SqlConnection(GetConnectionString());
                using SqlCommand command = new SqlCommand("dbo.GetActivePoStatuses", connection);
                command.CommandType = CommandType.StoredProcedure;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PoStatusMasterModel
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        StatusName = reader["StatusName"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error loading PO statuses: {ex.Message}"); }
            return list;
        }

        private async Task<List<ShipmentStatusMasterModel>> GetShipmentStatusesFromDb()
        {
            var list = new List<ShipmentStatusMasterModel>();
            try
            {
                using SqlConnection connection = new SqlConnection(GetConnectionString());
                using SqlCommand command = new SqlCommand("dbo.GetActiveShipmentStatuses", connection);
                command.CommandType = CommandType.StoredProcedure;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new ShipmentStatusMasterModel
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        StatusName = reader["StatusName"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error loading shipment statuses: {ex.Message}"); }
            return list;
        }

        private async Task<(List<PrPoItem> Records, int TotalRecords)>
            GetRecords(int year, int pageNumber, int pageSize)
        {
            var list = new List<PrPoItem>();
            int totalRecords = 0;

            try
            {
                using SqlConnection connection = new SqlConnection(GetConnectionString());
                using SqlCommand command = new SqlCommand("dbo.GetPrPoReport", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@Year", SqlDbType.Int).Value = year;
                command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
                command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

                SqlParameter totalParam = command.Parameters.Add("@TotalRecords", SqlDbType.Int);
                totalParam.Direction = ParameterDirection.Output;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(MapPrPo(reader));
                }

                await reader.CloseAsync();

                if (totalParam.Value != DBNull.Value)
                {
                    totalRecords = Convert.ToInt32(totalParam.Value);
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error loading records: {ex.Message}"); }

            return (list, totalRecords);
        }

        private PrPoItem MapPrPo(SqlDataReader reader)
        {
            return new PrPoItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                RequisitionDate = Convert.ToDateTime(reader["RequisitionDate"]),
                ProjectCode = reader["ProjectCode"]?.ToString() ?? "",
                PrNumber = reader["PrNumber"]?.ToString() ?? "",
                PoStatus = reader["PoStatus"]?.ToString() ?? "",
                PurchaseOriginRegion = reader["PurchaseOriginRegion"]?.ToString() ?? "",
                BuyerName = reader["BuyerName"]?.ToString() ?? "",
                AssetTarget = reader["AssetTarget"]?.ToString() ?? "",
                Quantity = Convert.ToInt32(reader["Quantity"]),
                UnitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                Subtotal = Convert.ToDecimal(reader["Subtotal"]),
                TaxPercent = Convert.ToDecimal(reader["TaxPercent"]),
                TaxAmount = Convert.ToDecimal(reader["TaxAmount"]),
                TotalCost = Convert.ToDecimal(reader["TotalCost"]),
                ShipmentTrackingNo = reader["ShipmentTrackingNo"] == DBNull.Value ? null : reader["ShipmentTrackingNo"].ToString(),
                DeliveryDate = reader["DeliveryDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["DeliveryDate"]),
                ShipmentStatus = reader["ShipmentStatus"] == DBNull.Value ? null : reader["ShipmentStatus"].ToString(),
                Remarks = reader["Remarks"] == DBNull.Value ? null : reader["Remarks"].ToString(),
                IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(reader["IsActive"]),
                CreatedBy = reader["CreatedBy"] == DBNull.Value ? null : reader["CreatedBy"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"]),
                ModifiedBy = reader["ModifiedBy"] == DBNull.Value ? null : reader["ModifiedBy"].ToString(),
                ModifiedDate = reader["ModifiedDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["ModifiedDate"])
            };
        }


        private string GetConnectionString()
        {
            string? cs = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("DefaultConnection was not found.");
            return cs;
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

            using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string item = reader["SpareItem"]?.ToString()?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(item))
                    descriptions.Add(item);
            }

            return descriptions;
        }


    }
}