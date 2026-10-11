using System.Text.Json.Serialization;

namespace SaBai.API.Domain;

public enum Gender
{
    [JsonStringEnumMemberName("male")]
    Male,

    [JsonStringEnumMemberName("female")]
    Female,
}

public enum StaffRole
{
    [JsonStringEnumMemberName("admin")]
    Admin,

    [JsonStringEnumMemberName("member")]
    Member,
}

public enum OrderStatus
{
    [JsonStringEnumMemberName("PENDING")]
    Pending,

    [JsonStringEnumMemberName("APPROVED")]
    Approved,

    [JsonStringEnumMemberName("CANCELLED")]
    Cancelled,
}

public enum StockStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("discarded")]
    Discarded,

    [JsonStringEnumMemberName("donated")]
    Donated,

    [JsonStringEnumMemberName("dispensed")]
    Dispensed,

    [JsonStringEnumMemberName("reserved")]
    Reserved,
}

public enum StockChangeField
{
    [JsonStringEnumMemberName("location")]
    Location,

    [JsonStringEnumMemberName("quantity")]
    Quantity,

    [JsonStringEnumMemberName("stock_status")]
    StockStatus,

    [JsonStringEnumMemberName("remarks")]
    Remarks,
}

public enum ReferralState
{
    New,
    Seen,
    Outgoing,
    Completed,
    CompletedSuccess,
    CompletedFailure,
    None,
}

public enum DiagnosisCategory
{
    Cardiovascular,
    Dermatology,

    [JsonStringEnumMemberName("Ear Nose Throat")]
    EarNoseThroat,

    Endocrine,
    Eye,
    Gastrointestinal,
    Haematology,

    [JsonStringEnumMemberName("Infectious Diseases")]
    InfectiousDiseases,

    [JsonStringEnumMemberName("Renal & Genitourinary")]
    RenalAndGenitourinary,

    Respiratory,
    Musculoskeletal,
    Neurology,

    [JsonStringEnumMemberName("Obstetrics & Gynaecology")]
    ObstetricsAndGynaecology,

    [JsonStringEnumMemberName("Oral Health")]
    OralHealth,

    Others,
}

public enum BroadcastSeverity
{
    [JsonStringEnumMemberName("info")]
    Info,

    [JsonStringEnumMemberName("warning")]
    Warning,

    [JsonStringEnumMemberName("critical")]
    Critical,
}
