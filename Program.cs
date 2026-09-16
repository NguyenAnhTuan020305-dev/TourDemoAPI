/*
================================================================================
  BACKEND: ASP.NET Core Minimal API - Demo Ràng buộc Dữ liệu
  
  Yêu cầu: .NET 8.0 SDK
  
  Cách chạy:
    dotnet new console -n TourDemoAPI
    cd TourDemoAPI
    dotnet add package Microsoft.Data.SqlClient
    dotnet add package Swashbuckle.AspNetCore
    (Copy file này đè Program.cs)
    dotnet run
    
  URL: http://localhost:5000
  Swagger: http://localhost:5000/swagger
================================================================================
*/

using Microsoft.Data.SqlClient;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình SQL Server - CHỈNH CHO PHÙ HỢP VỚI MÁY BẠN
string connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Server=.\\SQLEXPRESS;Database=TourManagement;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

var app = builder.Build();
app.UseCors();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => "Tour Demo API is running!");

// ============================================================
// HELPER: Tạo kết nối SQL
// ============================================================
SqlConnection GetConnection()
{
    var conn = new SqlConnection(connectionString);
    conn.Open();
    return conn;
}

string? DbString(SqlDataReader reader, string column)
{
    return reader[column] == DBNull.Value ? null : reader[column].ToString();
}

async Task<List<string>> ValidateTourItinerary(SqlConnection conn, string maTour)
{
    var errors = new List<string>();
    var requiredLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var usedLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    int? expectedDays = null;
    int actualDays = 0;

    using (var cmd = new SqlCommand("SELECT soNgay FROM Tour WHERE maTour = @maTour", conn))
    {
        cmd.Parameters.AddWithValue("@maTour", maTour);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
        {
            errors.Add($"Không tìm thấy tour {maTour}.");
            return errors;
        }
        expectedDays = Convert.ToInt32(result);
    }

    using (var cmd = new SqlCommand("SELECT maDiaDanh FROM TourDiaDanh WHERE maTour = @maTour", conn))
    {
        cmd.Parameters.AddWithValue("@maTour", maTour);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            requiredLocations.Add(reader["maDiaDanh"].ToString() ?? "");
        }
    }

    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM LichTrinhNgay WHERE maTour = @maTour", conn))
    {
        cmd.Parameters.AddWithValue("@maTour", maTour);
        actualDays = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    if (expectedDays != actualDays)
    {
        errors.Add($"Số ngày lịch trình ({actualDays}) không khớp với số ngày của tour ({expectedDays}).");
    }

    using (var cmd = new SqlCommand(@"
        SELECT DISTINCT cthd.maDiaDanh
        FROM LichTrinhNgay ltn
        JOIN ChiTietHoatDong cthd ON cthd.maLichTrinh = ltn.maLichTrinh
        WHERE ltn.maTour = @maTour AND cthd.maDiaDanh IS NOT NULL", conn))
    {
        cmd.Parameters.AddWithValue("@maTour", maTour);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var maDiaDanh = reader["maDiaDanh"].ToString() ?? "";
            usedLocations.Add(maDiaDanh);
            if (!requiredLocations.Contains(maDiaDanh))
            {
                errors.Add($"Địa danh {maDiaDanh} được dùng trong lịch trình nhưng không thuộc danh sách địa danh chính thức của tour.");
            }
        }
    }

    foreach (var maDiaDanh in requiredLocations.Where(maDiaDanh => !usedLocations.Contains(maDiaDanh)))
    {
        errors.Add($"Địa danh {maDiaDanh} thuộc tour nhưng chưa được sử dụng trong lịch trình.");
    }

    return errors;
}

// ============================================================
// API 1: Lấy danh sách Tỉnh/Thành phố
// ============================================================
app.MapGet("/api/provinces", async () =>
{
    var list = new List<object>();
    using var conn = GetConnection();
    using var cmd = new SqlCommand("SELECT maTinh, tenTinh, vungMien FROM TinhThanh ORDER BY maTinh", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        string tenTinh = reader["tenTinh"].ToString() ?? "";
        list.Add(new { maTinh = reader["maTinh"].ToString(), tenTinh = tenTinh, vungMien = reader["vungMien"].ToString() });
    }
    return Results.Ok(list);
});

// ============================================================
// API 2: Lấy danh sách Địa danh theo Tỉnh
// ============================================================
app.MapGet("/api/destinations/{maTinh}", async (string maTinh) =>
{
    var list = new List<object>();
    using var conn = GetConnection();
    using var cmd = new SqlCommand("SELECT maDiaDanh, maTinh, tenDiaDanh, diaChiChiTiet FROM DiaDanh WHERE maTinh = @maTinh ORDER BY tenDiaDanh", conn);
    cmd.Parameters.AddWithValue("@maTinh", maTinh);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        list.Add(new { maDiaDanh = reader["maDiaDanh"], maTinh = reader["maTinh"], tenDiaDanh = reader["tenDiaDanh"], diaChiChiTiet = reader["diaChiChiTiet"] });
    return Results.Ok(list);
});

// ============================================================
// API 3: Lưu lịch trình tour (INSERT Tour + LichTrinhNgay + ChiTietHoatDong)
// ============================================================
app.MapPost("/api/itinerary", async (ItineraryRequest req) =>
{
    using var conn = GetConnection();
    using var transaction = conn.BeginTransaction();
    try
    {
        // 1. INSERT Tour
        var tourSql = @"INSERT INTO Tour (maTour, tenTour, thoiLuong, soNgay, soDem, phuongTien, loaiHinhDuLich)
                        VALUES (@maTour, @tenTour, @thoiLuong, @soNgay, @soDem, @phuongTien, @loaiHinh)";
        using (var cmd = new SqlCommand(tourSql, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@maTour", req.maTour);
            cmd.Parameters.AddWithValue("@tenTour", req.tenTour);
            cmd.Parameters.AddWithValue("@thoiLuong", req.thoiLuong);
            cmd.Parameters.AddWithValue("@soNgay", req.soNgay);
            cmd.Parameters.AddWithValue("@soDem", req.soDem);
            cmd.Parameters.AddWithValue("@phuongTien", (object?)req.phuongTien ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@loaiHinh", (object?)req.loaiHinh ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. INSERT LichTrinhNgay + ChiTietHoatDong
        foreach (var day in req.days)
        {
            var ltSql = @"INSERT INTO LichTrinhNgay (maLichTrinh, maTour, soThuTuNgay, tieuDeNgay, khachSan)
                          VALUES (@maLichTrinh, @maTour, @soThuTuNgay, @tieuDeNgay, @khachSan)";
            using (var cmd = new SqlCommand(ltSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@maLichTrinh", day.maLichTrinh);
                cmd.Parameters.AddWithValue("@maTour", req.maTour);
                cmd.Parameters.AddWithValue("@soThuTuNgay", day.soThuTuNgay);
                cmd.Parameters.AddWithValue("@tieuDeNgay", day.tieuDeNgay);
                cmd.Parameters.AddWithValue("@khachSan", (object?)day.khachSan ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var act in day.activities)
            {
                var actSql = @"INSERT INTO ChiTietHoatDong (maLichTrinh, khungGio, maDiaDanh, noiDungHoatDong, buaAn)
                               VALUES (@maLichTrinh, @khungGio, @maDiaDanh, @noiDung, @buaAn)";
                using (var cmd = new SqlCommand(actSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@maLichTrinh", day.maLichTrinh);
                    cmd.Parameters.AddWithValue("@khungGio", act.khungGio);
                    cmd.Parameters.AddWithValue("@maDiaDanh", (object?)act.maDiaDanh ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@noiDung", act.noiDungHoatDong);
                    cmd.Parameters.AddWithValue("@buaAn", (object?)act.buaAn ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        transaction.Commit();
        return Results.Ok(new { success = true, message = "Lưu lịch trình thành công!" });
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

// ============================================================
// API 3b: Lay danh sach Tour
// ============================================================
app.MapGet("/api/tours", async () =>
{
    var list = new List<object>();
    using var conn = GetConnection();
    using var cmd = new SqlCommand(@"
        SELECT t.maTour, t.tenTour, t.thoiLuong, t.soNgay, t.soDem, t.phuongTien,
               t.loaiHinhDuLich, t.moTaTongQuan, t.giaNguoiLon, t.giaTreEm,
               COUNT(tdd.maDiaDanh) AS soDiaDanh
        FROM Tour t
        LEFT JOIN TourDiaDanh tdd ON tdd.maTour = t.maTour
        GROUP BY t.maTour, t.tenTour, t.thoiLuong, t.soNgay, t.soDem, t.phuongTien,
                 t.loaiHinhDuLich, t.moTaTongQuan, t.giaNguoiLon, t.giaTreEm
        ORDER BY t.maTour", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        list.Add(new {
            maTour = DbString(reader, "maTour"),
            tenTour = DbString(reader, "tenTour"),
            thoiLuong = DbString(reader, "thoiLuong"),
            soNgay = reader["soNgay"],
            soDem = reader["soDem"],
            phuongTien = DbString(reader, "phuongTien"),
            loaiHinh = DbString(reader, "loaiHinhDuLich"),
            moTa = DbString(reader, "moTaTongQuan"),
            giaNguoiLon = reader["giaNguoiLon"],
            giaTreEm = reader["giaTreEm"],
            soDiaDanh = reader["soDiaDanh"]
        });
    return Results.Ok(list);
});

app.MapGet("/api/tours/{maTour}/brochure", async (string maTour) =>
{
    using var conn = GetConnection();

    using var cmdTour = new SqlCommand(@"
        SELECT maTour, tenTour, thoiLuong, soNgay, soDem, phuongTien, loaiHinhDuLich,
               moTaTongQuan, giaNguoiLon, giaTreEm
        FROM Tour
        WHERE maTour = @maTour", conn);
    cmdTour.Parameters.AddWithValue("@maTour", maTour);
    using var readerTour = await cmdTour.ExecuteReaderAsync();
    if (!await readerTour.ReadAsync()) return Results.NotFound();
    var tour = new
    {
        maTour = DbString(readerTour, "maTour"),
        tenTour = DbString(readerTour, "tenTour"),
        thoiLuong = DbString(readerTour, "thoiLuong"),
        soNgay = readerTour["soNgay"],
        soDem = readerTour["soDem"],
        phuongTien = DbString(readerTour, "phuongTien"),
        loaiHinh = DbString(readerTour, "loaiHinhDuLich"),
        moTa = DbString(readerTour, "moTaTongQuan"),
        giaNguoiLon = readerTour["giaNguoiLon"],
        giaTreEm = readerTour["giaTreEm"]
    };
    readerTour.Close();

    var locations = new List<object>();
    using (var cmdLocations = new SqlCommand(@"
        SELECT tdd.maDiaDanh, tdd.thuTuThamQuan, dd.tenDiaDanh, dd.maTinh, dd.diaChiChiTiet
        FROM TourDiaDanh tdd
        JOIN DiaDanh dd ON dd.maDiaDanh = tdd.maDiaDanh
        WHERE tdd.maTour = @maTour
        ORDER BY tdd.thuTuThamQuan", conn))
    {
        cmdLocations.Parameters.AddWithValue("@maTour", maTour);
        using var readerLocations = await cmdLocations.ExecuteReaderAsync();
        while (await readerLocations.ReadAsync())
        {
            locations.Add(new
            {
                maDiaDanh = DbString(readerLocations, "maDiaDanh"),
                tenDiaDanh = DbString(readerLocations, "tenDiaDanh"),
                maTinh = DbString(readerLocations, "maTinh"),
                diaChiChiTiet = DbString(readerLocations, "diaChiChiTiet"),
                thuTuThamQuan = readerLocations["thuTuThamQuan"]
            });
        }
    }

    var days = new List<object>();
    using (var cmdDays = new SqlCommand(@"
        SELECT maLichTrinh, maTour, soThuTuNgay, tieuDeNgay, khachSan
        FROM LichTrinhNgay
        WHERE maTour = @maTour
        ORDER BY soThuTuNgay", conn))
    {
        cmdDays.Parameters.AddWithValue("@maTour", maTour);
        using var readerDays = await cmdDays.ExecuteReaderAsync();
        while (await readerDays.ReadAsync())
        {
            days.Add(new
            {
                maLichTrinh = DbString(readerDays, "maLichTrinh"),
                maTour = DbString(readerDays, "maTour"),
                soThuTuNgay = readerDays["soThuTuNgay"],
                tieuDeNgay = DbString(readerDays, "tieuDeNgay"),
                khachSan = DbString(readerDays, "khachSan")
            });
        }
    }

    var activities = new List<object>();
    using (var cmdActivities = new SqlCommand(@"
        SELECT ltn.maLichTrinh, ltn.soThuTuNgay, cthd.khungGio, cthd.maDiaDanh,
               dd.tenDiaDanh, cthd.noiDungHoatDong, cthd.buaAn
        FROM LichTrinhNgay ltn
        JOIN ChiTietHoatDong cthd ON cthd.maLichTrinh = ltn.maLichTrinh
        LEFT JOIN DiaDanh dd ON dd.maDiaDanh = cthd.maDiaDanh
        WHERE ltn.maTour = @maTour
        ORDER BY ltn.soThuTuNgay, cthd.khungGio", conn))
    {
        cmdActivities.Parameters.AddWithValue("@maTour", maTour);
        using var readerActivities = await cmdActivities.ExecuteReaderAsync();
        while (await readerActivities.ReadAsync())
        {
            activities.Add(new
            {
                maLichTrinh = DbString(readerActivities, "maLichTrinh"),
                soThuTuNgay = readerActivities["soThuTuNgay"],
                khungGio = DbString(readerActivities, "khungGio"),
                maDiaDanh = DbString(readerActivities, "maDiaDanh"),
                tenDiaDanh = DbString(readerActivities, "tenDiaDanh"),
                noiDung = DbString(readerActivities, "noiDungHoatDong"),
                buaAn = DbString(readerActivities, "buaAn")
            });
        }
    }

    return Results.Ok(new { tour, locations, days, activities });
});

app.MapPost("/api/tours/{maTour}/validate-itinerary", async (string maTour) =>
{
    using var conn = GetConnection();
    var errors = await ValidateTourItinerary(conn, maTour);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { success = false, errors });
    }

    return Results.Ok(new { success = true, errors = Array.Empty<string>() });
});

app.MapPost("/api/tours/full", async (FullTourRequest req) =>
{
    var errors = new List<string>();
    if (req.soNgay <= 0) errors.Add("Số ngày phải lớn hơn 0.");
    if (req.diaDanh is null || req.diaDanh.Count == 0) errors.Add("Tour phải có ít nhất một địa danh.");
    if (req.days is null) errors.Add("Danh sách ngày lịch trình không được để trống.");
    else if (req.days.Count != req.soNgay) errors.Add($"Số ngày lịch trình ({req.days.Count}) không khớp với số ngày của tour ({req.soNgay}).");

    if (req.days is not null)
    {
        foreach (var day in req.days)
        {
            if (day.activities is null)
            {
                errors.Add($"Ngày {day.soThuTuNgay} phải có danh sách hoạt động.");
            }
        }
    }

    if (req.diaDanh is not null && req.days is not null && req.days.All(day => day.activities is not null))
    {
        var officialLocations = req.diaDanh.Select(d => d.maDiaDanh).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedLocations = req.days
            .SelectMany(day => day.activities)
            .Where(activity => !string.IsNullOrWhiteSpace(activity.maDiaDanh))
            .Select(activity => activity.maDiaDanh!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var maDiaDanh in usedLocations.Where(maDiaDanh => !officialLocations.Contains(maDiaDanh)))
        {
            errors.Add($"Địa danh {maDiaDanh} được dùng trong lịch trình nhưng không thuộc danh sách địa danh chính thức của tour.");
        }

        foreach (var maDiaDanh in officialLocations.Where(maDiaDanh => !usedLocations.Contains(maDiaDanh)))
        {
            errors.Add($"Địa danh {maDiaDanh} thuộc tour nhưng chưa được sử dụng trong lịch trình.");
        }
    }

    if (errors.Count > 0)
    {
        return Results.BadRequest(new { success = false, errors });
    }

    var diaDanhRequests = req.diaDanh!;
    var dayRequests = req.days!;

    using var conn = GetConnection();
    using var transaction = conn.BeginTransaction();
    var committed = false;
    try
    {
        var tourSql = @"INSERT INTO Tour
            (maTour, tenTour, thoiLuong, soNgay, soDem, phuongTien, loaiHinhDuLich, moTaTongQuan, giaNguoiLon, giaTreEm)
            VALUES
            (@maTour, @tenTour, @thoiLuong, @soNgay, @soDem, @phuongTien, @loaiHinh, @moTa, @giaNguoiLon, @giaTreEm)";
        using (var cmd = new SqlCommand(tourSql, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@maTour", req.maTour);
            cmd.Parameters.AddWithValue("@tenTour", req.tenTour);
            cmd.Parameters.AddWithValue("@thoiLuong", req.thoiLuong);
            cmd.Parameters.AddWithValue("@soNgay", req.soNgay);
            cmd.Parameters.AddWithValue("@soDem", req.soDem);
            cmd.Parameters.AddWithValue("@phuongTien", (object?)req.phuongTien ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@loaiHinh", (object?)req.loaiHinh ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@moTa", (object?)req.moTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@giaNguoiLon", req.giaNguoiLon);
            cmd.Parameters.AddWithValue("@giaTreEm", req.giaTreEm);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var diaDanh in diaDanhRequests)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO TourDiaDanh (maTour, maDiaDanh, thuTuThamQuan)
                VALUES (@maTour, @maDiaDanh, @thuTuThamQuan)", conn, transaction);
            cmd.Parameters.AddWithValue("@maTour", req.maTour);
            cmd.Parameters.AddWithValue("@maDiaDanh", diaDanh.maDiaDanh);
            cmd.Parameters.AddWithValue("@thuTuThamQuan", diaDanh.thuTuThamQuan);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var day in dayRequests)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO LichTrinhNgay (maLichTrinh, maTour, soThuTuNgay, tieuDeNgay, khachSan)
                VALUES (@maLichTrinh, @maTour, @soThuTuNgay, @tieuDeNgay, @khachSan)", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@maLichTrinh", day.maLichTrinh);
                cmd.Parameters.AddWithValue("@maTour", req.maTour);
                cmd.Parameters.AddWithValue("@soThuTuNgay", day.soThuTuNgay);
                cmd.Parameters.AddWithValue("@tieuDeNgay", day.tieuDeNgay);
                cmd.Parameters.AddWithValue("@khachSan", (object?)day.khachSan ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var act in day.activities)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO ChiTietHoatDong (maLichTrinh, khungGio, maDiaDanh, noiDungHoatDong, buaAn)
                    VALUES (@maLichTrinh, @khungGio, @maDiaDanh, @noiDung, @buaAn)", conn, transaction);
                cmd.Parameters.AddWithValue("@maLichTrinh", day.maLichTrinh);
                cmd.Parameters.AddWithValue("@khungGio", act.khungGio);
                cmd.Parameters.AddWithValue("@maDiaDanh", (object?)act.maDiaDanh ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@noiDung", act.noiDungHoatDong);
                cmd.Parameters.AddWithValue("@buaAn", (object?)act.buaAn ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        transaction.Commit();
        committed = true;

        return Results.Ok(new { success = true, message = "Tạo tour đầy đủ thành công!" });
    }
    catch (Exception ex)
    {
        if (!committed) transaction.Rollback();
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

// ============================================================
// API 3c: Lay chi tiet Tour + Lich trinh
// ============================================================
app.MapGet("/api/tours/{maTour}/itinerary", async (string maTour) =>
{
    using var conn = GetConnection();

    // Lay thong tin tour
    using var cmdTour = new SqlCommand("SELECT * FROM Tour WHERE maTour = @ma", conn);
    cmdTour.Parameters.AddWithValue("@ma", maTour);
    using var readerTour = await cmdTour.ExecuteReaderAsync();
    if (!await readerTour.ReadAsync()) return Results.NotFound();
    var tour = new {
        maTour = readerTour["maTour"]?.ToString(),
        tenTour = readerTour["tenTour"]?.ToString(),
        thoiLuong = readerTour["thoiLuong"]?.ToString(),
        moTa = readerTour["moTaTongQuan"]?.ToString()
    };
    readerTour.Close();

    // Lay lich trinh ngay
    var days = new List<object>();
    using var cmdDays = new SqlCommand("SELECT * FROM LichTrinhNgay WHERE maTour = @ma ORDER BY soThuTuNgay", conn);
    cmdDays.Parameters.AddWithValue("@ma", maTour);
    using var readerDays = await cmdDays.ExecuteReaderAsync();
    var dayIds = new List<string>();
    while (await readerDays.ReadAsync())
    {
        string dayId = readerDays["maLichTrinh"]?.ToString() ?? "";
        dayIds.Add(dayId);
        days.Add(new {
            maLichTrinh = dayId,
            soThuTuNgay = readerDays["soThuTuNgay"],
            tieuDeNgay = readerDays["tieuDeNgay"]?.ToString(),
            khachSan = readerDays["khachSan"]?.ToString()
        });
    }
    readerDays.Close();

    // Lay chi tiet hoat dong cho tung ngay
    var allActivities = new Dictionary<string, List<object>>();
    foreach (var dayId in dayIds)
    {
        var acts = new List<object>();
        using var cmdAct = new SqlCommand(@"
            SELECT cthd.*, dd.tenDiaDanh
            FROM ChiTietHoatDong cthd
            LEFT JOIN DiaDanh dd ON cthd.maDiaDanh = dd.maDiaDanh
            WHERE cthd.maLichTrinh = @maLT
            ORDER BY cthd.khungGio", conn);
        cmdAct.Parameters.AddWithValue("@maLT", dayId);
        using var readerAct = await cmdAct.ExecuteReaderAsync();
        while (await readerAct.ReadAsync())
            acts.Add(new {
                khungGio = readerAct["khungGio"]?.ToString(),
                maDiaDanh = readerAct["maDiaDanh"]?.ToString(),
                tenDiaDanh = readerAct["tenDiaDanh"]?.ToString(),
                noiDung = readerAct["noiDungHoatDong"]?.ToString(),
                buaAn = readerAct["buaAn"]?.ToString()
            });
        allActivities[dayId] = acts;
    }

    return Results.Ok(new { tour, days, activities = allActivities });
});

// ============================================================
// API 4: Lay thong tin Chuyen di
// ============================================================
app.MapGet("/api/chuyendi/{maChuyenDi}", async (string maChuyenDi) =>
{
    using var conn = GetConnection();
    using var cmd = new SqlCommand(@"
        SELECT cd.*, t.tenTour
        FROM ChuyenDi cd JOIN Tour t ON cd.maTour = t.maTour
        WHERE cd.maChuyenDi = @maChuyenDi", conn);
    cmd.Parameters.AddWithValue("@maChuyenDi", maChuyenDi);
    using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        return Results.Ok(new
        {
            maChuyenDi = reader["maChuyenDi"],
            tenTour = reader["tenTour"],
            ngayKhoiHanh = ((DateTime)reader["ngayKhoiHanh"]).ToString("dd/MM/yyyy"),
            giaNguoiLon = reader["giaNguoiLon"],
            giaTreEm = reader["giaTreEm"],
            soChoToiDa = reader["soChoToiDa"],
            soChoConLai = reader["soChoConLai"],
            trangThai = reader["trangThai"]
        });
    }
    return Results.NotFound();
});

// ============================================================
// API 5: Tính tiền đặt chỗ
// ============================================================
app.MapPost("/api/booking/calculate", async (BookingCalcRequest req) =>
{
    // CHECK: soNguoiLon > 0
    if (req.soNguoiLon <= 0)
        return Results.BadRequest(new { success = false, message = "Số người lớn phải > 0! (CHECK constraint)" });

    using var conn = GetConnection();

    // Lấy giá tour
    decimal giaNguoiLon = 0;
    using (var cmd = new SqlCommand("SELECT cd.giaNguoiLon FROM ChuyenDi cd WHERE cd.maChuyenDi = @ma", conn))
    {
        cmd.Parameters.AddWithValue("@ma", req.maChuyenDi);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return Results.NotFound(new { success = false, message = $"Không tìm thấy chuyến đi {req.maChuyenDi}." });
        giaNguoiLon = Convert.ToDecimal(result);
    }

    decimal tongTienGoc = req.soNguoiLon * giaNguoiLon;
    decimal tienGiam = 0;
    string voucherMsg = "";

    if (!string.IsNullOrEmpty(req.maVoucher))
    {
        using var cmd = new SqlCommand("SELECT phanTramGiam, soTienGiamToiDa FROM KhuyenMai WHERE maVoucher = @ma", conn);
        cmd.Parameters.AddWithValue("@ma", req.maVoucher);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int phanTram = Convert.ToInt32(reader["phanTramGiam"]);
            decimal mucGiam = Convert.ToDecimal(reader["soTienGiamToiDa"]);
            tienGiam = Math.Min(tongTienGoc * phanTram / 100, mucGiam);
            voucherMsg = $"Voucher {req.maVoucher}: Giảm {phanTram}% = {tienGiam:N0} VNĐ";
        }
    }

    decimal tongTienPhaiTra = tongTienGoc - tienGiam;
    decimal tienCoc = Math.Round(tongTienPhaiTra * 0.30m);
    decimal tienConLai = tongTienPhaiTra - tienCoc;
    DateTime hanChot = DateTime.Parse("2026-10-15").AddDays(-10);

    return Results.Ok(new
    {
        tongTienGoc, tienGiam, tongTienPhaiTra, tienCoc, tienConLai,
        hanChotThanhToan = hanChot.ToString("dd/MM/yyyy"),
        voucherMsg
    });
});

// ============================================================
// API 6: Xác nhận đặt tour + nộp cọc
// ============================================================
app.MapPost("/api/booking/confirm", async (BookingConfirmRequest req) =>
{
    // CHECK: tienCoc <= tongTienPhaiTra
    if (req.tienCoc > req.tongTienPhaiTra)
        return Results.BadRequest(new { success = false, message = $"Tiền cọc {req.tienCoc:N0} không được lớn hơn tổng tiền {req.tongTienPhaiTra:N0}! (CHECK constraint)" });

    // CHECK: tienCoc >= 0
    if (req.tienCoc < 0)
        return Results.BadRequest(new { success = false, message = "Tiền cọc không được âm! (CHECK constraint)" });

    using var conn = GetConnection();
    using var transaction = conn.BeginTransaction();
    var committed = false;
    try
    {
        DateTime now = DateTime.Now;
        DateTime hanChot = DateTime.Parse("2026-10-15").AddDays(-10);

        // 1. INSERT PhieuDangKyTour
        var donSql = @"INSERT INTO PhieuDangKyTour
            (maDon, maChuyenDi, maKH, maVoucher, ngayDat, tongSoKhach, tongTienGoc,
             tienGiamVoucher, tongTienPhaiTra, tienCocToiThieu, soTienDaThanhToan, tienConLai,
             hanChotThanhToan, trangThai)
            VALUES
            (@maDon, @maChuyenDi, @maKH, @maVoucher, @ngayDat, @tongSoKhach, @tongTienGoc,
             @tienGiamVoucher, @tongTienPhaiTra, @tienCoc, @tienCoc, @tienConLai,
             @hanChot, N'Đã cọc 30%')";
        using (var cmd = new SqlCommand(donSql, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@maDon", req.maDon);
            cmd.Parameters.AddWithValue("@maChuyenDi", req.maChuyenDi);
            cmd.Parameters.AddWithValue("@maKH", req.maKH);
            cmd.Parameters.AddWithValue("@maVoucher", (object?)req.maVoucher ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ngayDat", now);
            cmd.Parameters.AddWithValue("@tongSoKhach", req.soNguoiLon);
            cmd.Parameters.AddWithValue("@tongTienGoc", req.tongTienGoc);
            cmd.Parameters.AddWithValue("@tienGiamVoucher", req.tienGiamVoucher);
            cmd.Parameters.AddWithValue("@tongTienPhaiTra", req.tongTienPhaiTra);
            cmd.Parameters.AddWithValue("@tienCoc", req.tienCoc);
            cmd.Parameters.AddWithValue("@tienConLai", req.tienConLai);
            cmd.Parameters.AddWithValue("@hanChot", hanChot);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. INSERT HanhKhach
        foreach (var hk in req.hanhKhach)
        {
            using var cmd = new SqlCommand(
                "INSERT INTO HanhKhach (maDon, hoTen, loaiKhach) VALUES (@maDon, @hoTen, @loaiKhach)", conn, transaction);
            cmd.Parameters.AddWithValue("@maDon", req.maDon);
            cmd.Parameters.AddWithValue("@hoTen", hk.hoTen);
            cmd.Parameters.AddWithValue("@loaiKhach", hk.loaiKhach);
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. INSERT PhieuThanhToan
        using (var cmd = new SqlCommand(
            @"INSERT INTO PhieuThanhToan (maGiaoDich, maDon, soTien, ngayThanhToan, loaiThanhToan, hinhThucThanhToan, trangThai)
              VALUES (@maGD, @maDon, @soTien, @ngay, N'Đặt cọc 30%', N'VNPay', N'Thành công')", conn, transaction))
        {
            cmd.Parameters.AddWithValue("@maGD", "VNP_" + now.Ticks);
            cmd.Parameters.AddWithValue("@maDon", req.maDon);
            cmd.Parameters.AddWithValue("@soTien", req.tienCoc);
            cmd.Parameters.AddWithValue("@ngay", now);
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. UPDATE ChuyenDi: trừ chỗ
        using (var cmd = new SqlCommand(
            "UPDATE ChuyenDi SET soChoConLai = soChoConLai - @sl WHERE maChuyenDi = @ma AND soChoConLai >= @sl", conn, transaction))
        {
            cmd.Parameters.AddWithValue("@ma", req.maChuyenDi);
            cmd.Parameters.AddWithValue("@sl", req.soNguoiLon);
            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) throw new Exception("Không đủ chỗ trống!");
        }

        // 5. UPDATE KhuyenMai: tăng lượt dùng
        if (!string.IsNullOrEmpty(req.maVoucher))
        {
            using var cmd = new SqlCommand(
                "UPDATE KhuyenMai SET soLuongDaDung = soLuongDaDung + 1 WHERE maVoucher = @ma", conn, transaction);
            cmd.Parameters.AddWithValue("@ma", req.maVoucher);
            await cmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();
        committed = true;

        // Lấy số chỗ còn lại
        using var cmdCheck = new SqlCommand("SELECT soChoConLai FROM ChuyenDi WHERE maChuyenDi = @ma", conn);
        cmdCheck.Parameters.AddWithValue("@ma", req.maChuyenDi);
        var soCho = await cmdCheck.ExecuteScalarAsync();

        return Results.Ok(new { success = true, message = "Thanh toán cọc thành công!", soChoConLai = soCho });
    }
    catch (Exception ex)
    {
        if (!committed) transaction.Rollback();
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

// ============================================================
// API 7: Gửi đánh giá + AI phân tích cảm xúc
// ============================================================
app.MapPost("/api/review", async (ReviewRequest req) =>
{
    using var conn = GetConnection();

    // CHECK: Mỗi đơn chỉ đánh giá 1 lần
    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM PhanHoiDanhGia WHERE maDon = @ma", conn))
    {
        cmd.Parameters.AddWithValue("@ma", req.maDon);
        int exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (exists > 0)
            return Results.BadRequest(new { success = false, message = "Đơn này đã được đánh giá rồi! (RULE: Mỗi đơn chỉ 1 lần)" });
    }

    // CHECK: diemSao BETWEEN 1 AND 5
    if (req.diemSao < 1 || req.diemSao > 5)
        return Results.BadRequest(new { success = false, message = $"diemSao = {req.diemSao} không hợp lệ! (CHECK: BETWEEN 1 AND 5)" });

    // CHECK: noiDungNhanXet không rỗng
    if (string.IsNullOrWhiteSpace(req.noiDungNhanXet))
        return Results.BadRequest(new { success = false, message = "Nội dung nhận xét không được để trống! (CHECK: NOT NULL)" });

    // AI phân tích cảm xúc
    var sentiment = AnalyzeSentiment(req.noiDungNhanXet);

    string maDanhGia = "DG" + DateTime.Now.Ticks.ToString()[^4..];
    using var cmdInsert = new SqlCommand(
        @"INSERT INTO PhanHoiDanhGia (maDanhGia, maDon, diemSao, noiDungNhanXet, nhanCamXucAI, doTinCayAI, ngayDanhGia)
          VALUES (@ma, @maDon, @diemSao, @noiDung, @nhanCamXuc, @doTinCay, @ngay)", conn);
    cmdInsert.Parameters.AddWithValue("@ma", maDanhGia);
    cmdInsert.Parameters.AddWithValue("@maDon", req.maDon);
    cmdInsert.Parameters.AddWithValue("@diemSao", req.diemSao);
    cmdInsert.Parameters.AddWithValue("@noiDung", req.noiDungNhanXet);
    cmdInsert.Parameters.AddWithValue("@nhanCamXuc", sentiment.Label);
    cmdInsert.Parameters.AddWithValue("@doTinCay", sentiment.Confidence);
    cmdInsert.Parameters.AddWithValue("@ngay", DateTime.Now);
    await cmdInsert.ExecuteNonQueryAsync();

    return Results.Ok(new
    {
        success = true,
        maDanhGia,
        nhanCamXucAI = sentiment.Label,
        doTinCayAI = sentiment.Confidence
    });
});

// ============================================================
// API 8: Lấy lịch sử đánh giá
// ============================================================
app.MapGet("/api/reviews", async () =>
{
    var list = new List<object>();
    using var conn = GetConnection();
    using var cmd = new SqlCommand("SELECT * FROM PhanHoiDanhGia ORDER BY ngayDanhGia DESC", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        list.Add(new
        {
            maDanhGia = reader["maDanhGia"],
            maDon = reader["maDon"],
            diemSao = reader["diemSao"],
            noiDungNhanXet = reader["noiDungNhanXet"],
            nhanCamXucAI = reader["nhanCamXucAI"],
            doTinCayAI = reader["doTinCayAI"],
            ngayDanhGia = ((DateTime)reader["ngayDanhGia"]).ToString("dd/MM/yyyy HH:mm")
        });
    return Results.Ok(list);
});

// ============================================================
// AI SENTIMENT (Rule-based)
// ============================================================
static SentimentResult AnalyzeSentiment(string text)
{
    string[] positive = { "tuyệt", "tốt", "ngon", "vui", "hài lòng", "thích", "đẹp", "nhiệt tình",
        "tuyệt vời", "hay", "thoải mái", "sạch", "an toàn", "chu đáo", "đáng" };
    string[] negative = { "tệ", "dở", "chán", "bẩn", "kém", "tồi", "đắt", "ồn", "chậm", "thất vọng" };

    string lower = text.ToLower();
    int pos = positive.Count(w => lower.Contains(w));
    int neg = negative.Count(w => lower.Contains(w));

    string label;
    double confidence;

    if (pos > neg) { label = "Tích cực"; confidence = 0.80 + (double)pos / (pos + neg + 2) * 0.18; }
    else if (neg > pos) { label = "Tiêu cực"; confidence = 0.75 + (double)neg / (pos + neg + 2) * 0.20; }
    else { label = "Trung lập"; confidence = 0.55; }

    return new SentimentResult { Label = label, Confidence = Math.Min(confidence, 0.99) };
}

app.Run();

// ============================================================
// REQUEST MODELS
// ============================================================
record ItineraryRequest(string maTour, string tenTour, string thoiLuong, int soNgay, int soDem,
    string? phuongTien, string? loaiHinh, List<DaySchedule> days);
record FullTourRequest(
    string maTour,
    string tenTour,
    string thoiLuong,
    int soNgay,
    int soDem,
    string? phuongTien,
    string? loaiHinh,
    string? moTa,
    decimal giaNguoiLon,
    decimal giaTreEm,
    List<TourLocationRequest> diaDanh,
    List<DaySchedule> days
);
record TourLocationRequest(string maDiaDanh, int thuTuThamQuan);
record DaySchedule(string maLichTrinh, int soThuTuNgay, string tieuDeNgay, string? khachSan, List<Activity> activities);
record Activity(string khungGio, string? maDiaDanh, string noiDungHoatDong, string? buaAn);

record BookingCalcRequest(string maChuyenDi, int soNguoiLon, string? maVoucher);

record BookingConfirmRequest(string maDon, string maChuyenDi, string maKH, string? maVoucher,
    int soNguoiLon, decimal tongTienGoc, decimal tienGiamVoucher, decimal tongTienPhaiTra,
    decimal tienCoc, decimal tienConLai, List<HanhKhachRequest> hanhKhach);
record HanhKhachRequest(string hoTen, string loaiKhach);

record ReviewRequest(string maDon, int diemSao, string noiDungNhanXet);

record SentimentResult { public string Label { get; set; } = ""; public double Confidence { get; set; } }
