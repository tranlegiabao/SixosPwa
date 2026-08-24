
$content = Get-Content -Path "SixosPwa/Areas/Admin/Models/AdminViewModels.cs" -Raw
$content = $content -replace "public int AccountCount \{ get; init;`r`n    public IReadOnlyList<FacilityGroupStat> FacilityGroupStats \{ get; init; \} = Array.Empty<FacilityGroupStat>\(\); \}", "    public int AccountCount { get; init; }"
$content = $content -replace "public int AccountCount \{ get; init; \}", "    public int AccountCount { get; init; }`n    public IReadOnlyList<FacilityGroupStat> FacilityGroupStats { get; init; } = Array.Empty<FacilityGroupStat>();"
Set-Content -Path "SixosPwa/Areas/Admin/Models/AdminViewModels.cs" -Value $content

