using SixosPwa.Models;

namespace SixosPwa.Services;

public interface IThietBiService
{
    Task<bool> LuuHoacCapNhatAsync(string sdt, string idThietBi, string tenThietBi);
    Task<ThietBi?> LayTheoSdtVaIdAsync(string sdt, string idThietBi);
}