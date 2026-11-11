package sabai.api.domain

import com.fasterxml.jackson.annotation.JsonProperty

// Constants use the C# member names because that is what the database stores
// (EF Core's HasConversion<string>()); @JsonProperty gives the wire name, like
// [JsonStringEnumMemberName] does in C#.

enum class Gender {
    @JsonProperty("male") Male,
    @JsonProperty("female") Female,
}

enum class OrderStatus {
    @JsonProperty("PENDING") Pending,
    @JsonProperty("APPROVED") Approved,
    @JsonProperty("CANCELLED") Cancelled,
}

enum class ReferralState { New, Seen, Outgoing, Completed, CompletedSuccess, CompletedFailure, None }

enum class DiagnosisCategory(val label: String) {
    Cardiovascular("Cardiovascular"),
    Dermatology("Dermatology"),
    @JsonProperty("Ear Nose Throat") EarNoseThroat("Ear Nose Throat"),
    Endocrine("Endocrine"),
    Eye("Eye"),
    Gastrointestinal("Gastrointestinal"),
    Haematology("Haematology"),
    @JsonProperty("Infectious Diseases") InfectiousDiseases("Infectious Diseases"),
    @JsonProperty("Renal & Genitourinary") RenalAndGenitourinary("Renal & Genitourinary"),
    Respiratory("Respiratory"),
    Musculoskeletal("Musculoskeletal"),
    Neurology("Neurology"),
    @JsonProperty("Obstetrics & Gynaecology") ObstetricsAndGynaecology("Obstetrics & Gynaecology"),
    @JsonProperty("Oral Health") OralHealth("Oral Health"),
    Others("Others"),
}

enum class BroadcastSeverity {
    @JsonProperty("info") Info,
    @JsonProperty("warning") Warning,
    @JsonProperty("critical") Critical,
}

enum class AuditAction { Created, Updated, Deleted, SignedIn, SignInFailed, SignedOut }
