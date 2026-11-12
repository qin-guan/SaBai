use crate::wire_enum;

wire_enum! {
    pub enum Gender { Male = "male", Female = "female" }
}

wire_enum! {
    pub enum OrderStatus { Pending = "PENDING", Approved = "APPROVED", Cancelled = "CANCELLED" }
}

wire_enum! {
    pub enum StockStatus {
        Active = "active",
        Discarded = "discarded",
        Donated = "donated",
        Dispensed = "dispensed",
        Reserved = "reserved",
    }
}

wire_enum! {
    pub enum StockChangeField {
        Location = "location",
        Quantity = "quantity",
        StockStatus = "stock_status",
        Remarks = "remarks",
    }
}

wire_enum! {
    pub enum ReferralState {
        New = "New",
        Seen = "Seen",
        Outgoing = "Outgoing",
        Completed = "Completed",
        CompletedSuccess = "CompletedSuccess",
        CompletedFailure = "CompletedFailure",
        None = "None",
    }
}

wire_enum! {
    pub enum DiagnosisCategory {
        Cardiovascular = "Cardiovascular",
        Dermatology = "Dermatology",
        EarNoseThroat = "Ear Nose Throat",
        Endocrine = "Endocrine",
        Eye = "Eye",
        Gastrointestinal = "Gastrointestinal",
        Haematology = "Haematology",
        InfectiousDiseases = "Infectious Diseases",
        RenalAndGenitourinary = "Renal & Genitourinary",
        Respiratory = "Respiratory",
        Musculoskeletal = "Musculoskeletal",
        Neurology = "Neurology",
        ObstetricsAndGynaecology = "Obstetrics & Gynaecology",
        OralHealth = "Oral Health",
        Others = "Others",
    }
}

wire_enum! {
    pub enum BroadcastSeverity { Info = "info", Warning = "warning", Critical = "critical" }
}
