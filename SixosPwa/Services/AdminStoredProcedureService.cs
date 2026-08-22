using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;

namespace SixosPwa.Services;

public sealed record AdminStoredProcedureResult(int Code, string? Message)
{
    public bool Succeeded => Code == 1;
}

public sealed class AdminStoredProcedureService
{
    private readonly ApplicationDbContext _db;

    public AdminStoredProcedureService(ApplicationDbContext db) => _db = db;

    public Task<AdminStoredProcedureResult> SaveTaiKhoanAsync(
        long id,
        string sdt,
        string role) =>
        ExecuteAsync("dbo.Admin_TaiKhoan_Save", command =>
        {
            AddParameter(command, "@Id", DbType.Int64, id);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@Role", DbType.String, role, 50);
        });

    public Task<AdminStoredProcedureResult> SaveDoiTacAsync(
        DoiTacEditViewModel model,
        bool updatePassword) =>
        ExecuteAsync("dbo.Admin_DoiTac_Save", command =>
        {
            AddParameter(command, "@Id", DbType.Int64, model.Id);
            AddParameter(command, "@MaDT", DbType.String, model.MaDT, 20);
            AddParameter(command, "@TenDT", DbType.String, model.TenDT, 100);
            AddParameter(command, "@DiaChi", DbType.String, model.DiaChi, 255);
            AddParameter(command, "@SDT", DbType.AnsiString, model.SDT, 20);
            AddParameter(command, "@Email", DbType.AnsiString, model.Email, 100);
            AddParameter(command, "@BrandName", DbType.String, model.BrandName, 100);
            AddParameter(command, "@Password", DbType.String, updatePassword ? model.Password : null, 255);
            AddParameter(command, "@UpdatePassword", DbType.Boolean, updatePassword);
        });

    public Task<AdminStoredProcedureResult> SaveCoSoYTeAsync(
        CoSoYTeEditViewModel model) =>
        ExecuteAsync("dbo.Admin_CoSoYTe_Save", command =>
        {
            AddParameter(command, "@Id", DbType.Int64, model.Id);
            AddParameter(command, "@MaCoSo", DbType.String, model.MaCoSo, 10);
            AddParameter(command, "@TenCoSo", DbType.String, model.TenCoSo, 100);
            AddParameter(command, "@DiaChi", DbType.String, model.DiaChi, 255);
            AddParameter(command, "@SoToaNha", DbType.String, model.SoToaNha, 100);
            AddParameter(command, "@Tinh", DbType.Int32, model.Tinh);
            AddParameter(command, "@Huyen", DbType.Int32, model.Huyen);
            AddParameter(command, "@PhuongXa", DbType.Int32, model.PhuongXa);
            AddParameter(command, "@LoaiCS", DbType.String, model.LoaiCS, 20);
            AddParameter(command, "@TGLamViec", DbType.String, model.TGLamViec, 50);
            AddParameter(command, "@XacMinh", DbType.Int32, model.XacMinh ? 1 : 0);
            AddParameter(command, "@Img", DbType.String, model.Img, 500);
            AddParameter(command, "@Logo", DbType.String, model.Logo, size: -1);
            AddParameter(command, "@QuangCao", DbType.Decimal, model.QuangCao, precision: 15, scale: 0);
            AddParameter(command, "@NoiDungQuangCao", DbType.String, model.NoiDungQuangCao, size: -1);
            AddParameter(command, "@QuangCaoImg", DbType.String, model.QuangCaoImg, size: -1);
            AddParameter(command, "@NoiDungGioiThieu", DbType.String, model.NoiDungGioiThieu, size: -1);
            AddParameter(command, "@NoiDungDichVu", DbType.String, model.NoiDungDichVu, size: -1);
            AddParameter(command, "@NoiDungDoiNgu", DbType.String, model.NoiDungDoiNgu, size: -1);
            AddParameter(command, "@NoiDungTrangThietBi", DbType.String, model.NoiDungTrangThietBi, size: -1);
            AddParameter(command, "@NoiDungLienHe", DbType.String, model.NoiDungLienHe, size: -1);
        });

    public Task<AdminStoredProcedureResult> SaveNoiDungCskcbAsync(
        string? maCoSo,
        string? tenCoSo,
        string loaiND,
        string? noiDung) =>
        ExecuteAsync("dbo.Admin_NDCSKCB_Save", command =>
        {
            AddParameter(command, "@MaCoSo", DbType.String, maCoSo, 10);
            AddParameter(command, "@TenCoSo", DbType.String, tenCoSo, 100);
            AddParameter(command, "@LoaiND", DbType.String, loaiND, 20);
            AddParameter(command, "@NoiDung", DbType.String, noiDung, size: -1);
        });

    public Task<string?> GetNoiDungCskcbAsync(
        string? maCoSo,
        string? tenCoSo,
        string loaiND) =>
        QueryStringAsync("dbo.Admin_NDCSKCB_Get", command =>
        {
            AddParameter(command, "@MaCoSo", DbType.String, maCoSo, 10);
            AddParameter(command, "@TenCoSo", DbType.String, tenCoSo, 100);
            AddParameter(command, "@LoaiND", DbType.String, loaiND, 20);
        });

    private async Task<AdminStoredProcedureResult> ExecuteAsync(
        string procedureName,
        Action<DbCommand> configure)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);

            var resultCode = AddOutputParameter(command, "@ResultCode", DbType.Int32, 4);
            var resultMessage = AddOutputParameter(command, "@ResultMessage", DbType.String, 4000);
            await command.ExecuteNonQueryAsync();

            var code = resultCode.Value == DBNull.Value ? 0 : Convert.ToInt32(resultCode.Value);
            var message = resultMessage.Value == DBNull.Value ? null : resultMessage.Value?.ToString();
            return new AdminStoredProcedureResult(code, message);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<string?> QueryStringAsync(
        string procedureName,
        Action<DbCommand> configure)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);

            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() && !await reader.IsDBNullAsync(0)
                ? reader.GetString(0)
                : null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static DbParameter AddOutputParameter(
        DbCommand command,
        string name,
        DbType type,
        int size)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Direction = ParameterDirection.Output;
        parameter.Size = size;
        command.Parameters.Add(parameter);
        return parameter;
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null,
        byte? precision = null,
        byte? scale = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
            parameter.Size = size.Value;
        if (precision.HasValue)
            parameter.Precision = precision.Value;
        if (scale.HasValue)
            parameter.Scale = scale.Value;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
