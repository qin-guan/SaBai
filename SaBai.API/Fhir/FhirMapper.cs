using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using SaBai.API.Domain;
using FhirPatient = Hl7.Fhir.Model.Patient;
using FhirEncounter = Hl7.Fhir.Model.Encounter;
using FhirObservation = Hl7.Fhir.Model.Observation;
using FhirCondition = Hl7.Fhir.Model.Condition;
using FhirAllergy = Hl7.Fhir.Model.AllergyIntolerance;
using FhirMedicationRequest = Hl7.Fhir.Model.MedicationRequest;
using FhirServiceRequest = Hl7.Fhir.Model.ServiceRequest;
using FhirDocumentReference = Hl7.Fhir.Model.DocumentReference;
using DomainPatient = SaBai.API.Domain.Patient;

namespace SaBai.API.Fhir;

/// <summary>
/// Maps SaBai domain entities to HL7 FHIR R4 resources using standard terminologies
/// (LOINC, UCUM, SNOMED CT / ICD-10, RxNorm).
/// </summary>
public static class FhirMapper
{
    public const string FhirJsonContentType = "application/fhir+json";
    private const string LoincSystem = "http://loinc.org";
    private const string UcumSystem = "http://unitsofmeasure.org";
    private const string PatientIdSystem = "https://sabai.clinic/identifiers/patient";
    private const string NationalIdSystem = "https://sabai.clinic/identifiers/national-id";

    private static readonly FhirJsonSerializer Serializer = new(
        new SerializerSettings { Pretty = false }
    );

    public static string Serialize(Resource resource) => Serializer.SerializeToString(resource);

    public static CapabilityStatement BuildCapabilityStatement(string baseUrl) =>
        new()
        {
            Status = PublicationStatus.Active,
            Date = DateTimeOffset.UtcNow.ToString("O"),
            Kind = CapabilityStatementKind.Instance,
            FhirVersion = FHIRVersion.N4_0_1,
            Format = ["json", FhirJsonContentType],
            Implementation = new CapabilityStatement.ImplementationComponent
            {
                Description = "SaBai Clinic HL7 FHIR R4 Facade",
                Url = baseUrl,
            },
            Rest =
            [
                new CapabilityStatement.RestComponent
                {
                    Mode = CapabilityStatement.RestfulCapabilityMode.Server,
                    Resource =
                    [
                        ResourceCapability(ResourceType.Patient, ["read", "search-type"]),
                        ResourceCapability(ResourceType.AllergyIntolerance, ["read"]),
                        ResourceCapability(ResourceType.Encounter, ["read"]),
                        ResourceCapability(ResourceType.Observation, ["read"]),
                        ResourceCapability(ResourceType.Condition, ["read"]),
                        ResourceCapability(ResourceType.MedicationRequest, ["read"]),
                        ResourceCapability(ResourceType.ServiceRequest, ["read"]),
                        ResourceCapability(ResourceType.DocumentReference, ["read"]),
                    ],
                },
            ],
        };

    private static CapabilityStatement.ResourceComponent ResourceCapability(
        ResourceType type,
        string[] interactions
    ) =>
        new()
        {
            Type = type.ToString(),
            Interaction =
            [
                .. interactions.Select(op => new CapabilityStatement.ResourceInteractionComponent
                {
                    CodeElement = new Code<CapabilityStatement.TypeRestfulInteraction>(
                        Enum.Parse<CapabilityStatement.TypeRestfulInteraction>(
                            op.Replace("-", "", StringComparison.Ordinal),
                            ignoreCase: true
                        )
                    ),
                }),
            ],
        };

    public static FhirPatient ToFhirPatient(DomainPatient p)
    {
        var patient = new FhirPatient
        {
            Id = p.Id.ToString(),
            Identifier =
            [
                new Identifier(
                    PatientIdSystem,
                    DomainPatient.FormatDisplayId(p.VillageCode?.Code, p.Number)
                ),
            ],
            Name = [new HumanName { Text = p.Name }],
            Gender = p.Gender switch
            {
                Gender.Male => AdministrativeGender.Male,
                Gender.Female => AdministrativeGender.Female,
                _ => AdministrativeGender.Unknown,
            },
            BirthDate = p.DateOfBirth.ToString("yyyy-MM-dd"),
        };

        if (!string.IsNullOrWhiteSpace(p.IdentificationNumber))
        {
            patient.Identifier.Add(new Identifier(NationalIdSystem, p.IdentificationNumber));
        }

        if (!string.IsNullOrWhiteSpace(p.ContactNo))
        {
            patient.Telecom =
            [
                new ContactPoint(
                    ContactPoint.ContactPointSystem.Phone,
                    ContactPoint.ContactPointUse.Mobile,
                    p.ContactNo
                ),
            ];
        }

        if (p.VillageCode is not null)
        {
            patient.Address =
            [
                new Address
                {
                    City = p.VillageCode.Name,
                    District = p.VillageCode.Code,
                },
            ];
        }

        return patient;
    }

    public static FhirAllergy? ToFhirAllergyIntolerance(DomainPatient p)
    {
        if (
            string.IsNullOrWhiteSpace(p.DrugAllergy)
            || p.DrugAllergy.Equals("none", StringComparison.OrdinalIgnoreCase)
            || p.DrugAllergy.Equals("nkda", StringComparison.OrdinalIgnoreCase)
            || p.DrugAllergy.Equals("nka", StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        return new FhirAllergy
        {
            Id = $"allergy-{p.Id}",
            ClinicalStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical",
                "active",
                "Active"
            ),
            Category = [FhirAllergy.AllergyIntoleranceCategory.Medication],
            Code = new CodeableConcept { Text = p.DrugAllergy },
            Patient = new ResourceReference($"Patient/{p.Id}"),
        };
    }

    public static FhirEncounter ToFhirEncounter(Visit visit) =>
        new()
        {
            Id = visit.Id.ToString(),
            Status = visit.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                ? FhirEncounter.EncounterStatus.Finished
                : FhirEncounter.EncounterStatus.InProgress,
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB", "ambulatory"),
            Subject = new ResourceReference($"Patient/{visit.PatientId}"),
            Period = new Period { Start = visit.Date.ToString("O") },
            Location = visit.VillageCode is null
                ? []
                : [new FhirEncounter.LocationComponent { Location = new ResourceReference { Display = visit.VillageCode.Name } }],
        };

    public static IEnumerable<FhirObservation> ToFhirVitalsObservations(Vitals v, Guid patientId)
    {
        var effective = v.RecordedAt?.ToString("O");
        var subject = new ResourceReference($"Patient/{patientId}");
        var encounter = new ResourceReference($"Encounter/{v.VisitId}");

        if (v.Height is { } h)
            yield return QuantityObs($"vitals-{v.Id}-height", "8302-2", "Body height", h, "cm", subject, encounter, effective);
        if (v.Weight is { } w)
            yield return QuantityObs($"vitals-{v.Id}-weight", "29463-7", "Body weight", w, "kg", subject, encounter, effective);
        if (v.Temperature is { } t)
            yield return QuantityObs($"vitals-{v.Id}-temp", "8310-5", "Body temperature", t, "Cel", subject, encounter, effective);
        if (v.HeartRate is { } hr)
            yield return QuantityObs($"vitals-{v.Id}-hr", "8867-4", "Heart rate", hr, "/min", subject, encounter, effective);
        if (v.Hba1c is { } a1c)
            yield return QuantityObs($"vitals-{v.Id}-hba1c", "4548-4", "Hemoglobin A1c/Hemoglobin.total in Blood", a1c, "%", subject, encounter, effective);
        if (v.BloodGlucoseFasting is { } bgf)
            yield return QuantityObs($"vitals-{v.Id}-bgf", "1558-6", "Fasting glucose [Mass/volume] in Serum or Plasma", bgf, "mg/dL", subject, encounter, effective);
        if (v.BloodGlucoseNonFasting is { } bgnf)
            yield return QuantityObs($"vitals-{v.Id}-bgnf", "2339-0", "Glucose [Mass/volume] in Blood", bgnf, "mg/dL", subject, encounter, effective);

        if (v.Systolic is not null || v.Diastolic is not null)
        {
            var bp = new FhirObservation
            {
                Id = $"vitals-{v.Id}-bp",
                Status = ObservationStatus.Final,
                Category = [VitalSignsCategory()],
                Code = new CodeableConcept(LoincSystem, "85354-9", "Blood pressure panel with all children optional"),
                Subject = subject,
                Encounter = encounter,
                Effective = effective is null ? null : new FhirDateTime(effective),
            };
            if (v.Systolic is { } sys)
            {
                bp.Component.Add(new FhirObservation.ComponentComponent
                {
                    Code = new CodeableConcept(LoincSystem, "8480-6", "Systolic blood pressure"),
                    Value = new Quantity(sys, "mm[Hg]", UcumSystem),
                });
            }
            if (v.Diastolic is { } dia)
            {
                bp.Component.Add(new FhirObservation.ComponentComponent
                {
                    Code = new CodeableConcept(LoincSystem, "8462-4", "Diastolic blood pressure"),
                    Value = new Quantity(dia, "mm[Hg]", UcumSystem),
                });
            }
            yield return bp;
        }
    }

    private static FhirObservation QuantityObs(
        string id,
        string loincCode,
        string display,
        decimal value,
        string ucumUnit,
        ResourceReference subject,
        ResourceReference encounter,
        string? effective
    ) =>
        new()
        {
            Id = id,
            Status = ObservationStatus.Final,
            Category = [VitalSignsCategory()],
            Code = new CodeableConcept(LoincSystem, loincCode, display),
            Subject = subject,
            Encounter = encounter,
            Effective = effective is null ? null : new FhirDateTime(effective),
            Value = new Quantity(value, ucumUnit, UcumSystem),
        };

    private static CodeableConcept VitalSignsCategory() =>
        new("http://terminology.hl7.org/CodeSystem/observation-category", "vital-signs", "Vital Signs");

    public static FhirCondition ToFhirCondition(Diagnosis d, Guid patientId, Guid visitId)
    {
        var code = new CodeableConcept { Text = d.Details };
        if (!string.IsNullOrWhiteSpace(d.CodingSystem) && !string.IsNullOrWhiteSpace(d.Code))
        {
            code.Coding.Add(new Coding(d.CodingSystem, d.Code, d.CodeDisplay ?? d.Details));
        }

        return new FhirCondition
        {
            Id = d.Id.ToString(),
            ClinicalStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/condition-clinical",
                "active",
                "Active"
            ),
            Code = code,
            Subject = new ResourceReference($"Patient/{patientId}"),
            Encounter = new ResourceReference($"Encounter/{visitId}"),
        };
    }

    public static FhirMedicationRequest ToFhirMedicationRequest(Order o, Guid patientId, Guid visitId)
    {
        var medConcept = new CodeableConcept
        {
            Text = o.MedicationBrand?.Name ?? $"Brand #{o.MedicationBrandId}",
        };
        if (o.MedicationBrand?.ActiveIngredient?.RxNormCode is { Length: > 0 } rx)
        {
            medConcept.Coding.Add(
                new Coding("http://www.nlm.nih.gov/research/umls/rxnorm", rx, o.MedicationBrand.ActiveIngredient.Name)
            );
        }

        return new FhirMedicationRequest
        {
            Id = o.Id.ToString(),
            Status = o.Status switch
            {
                OrderStatus.Approved => FhirMedicationRequest.MedicationrequestStatus.Completed,
                OrderStatus.Cancelled => FhirMedicationRequest.MedicationrequestStatus.Cancelled,
                _ => FhirMedicationRequest.MedicationrequestStatus.Active,
            },
            Intent = FhirMedicationRequest.MedicationRequestIntent.Order,
            Medication = medConcept,
            Subject = new ResourceReference($"Patient/{patientId}"),
            Encounter = new ResourceReference($"Encounter/{visitId}"),
            AuthoredOn = o.CreatedAt.ToString("O"),
            DosageInstruction = [new Dosage { Text = o.DosageInstructions }],
        };
    }

    public static FhirServiceRequest ToFhirServiceRequest(Referral r, Guid patientId, Guid visitId) =>
        new()
        {
            Id = r.Id.ToString(),
            Status = r.ReferralState switch
            {
                ReferralState.Completed or ReferralState.CompletedSuccess or ReferralState.CompletedFailure =>
                    RequestStatus.Completed,
                _ => RequestStatus.Active,
            },
            Intent = RequestIntent.Order,
            Code = new CodeableConcept { Text = r.ReferredFor },
            Subject = new ResourceReference($"Patient/{patientId}"),
            Encounter = new ResourceReference($"Encounter/{visitId}"),
            AuthoredOn = r.CreatedAt.ToString("O"),
            Note = string.IsNullOrWhiteSpace(r.ReferralNotes)
                ? []
                : [new Annotation { Text = new Markdown(r.ReferralNotes) }],
        };

    public static FhirDocumentReference ToFhirDocumentReference(StoredFile file) =>
        new()
        {
            Id = file.Id.ToString(),
            Status = DocumentReferenceStatus.Current,
            Description = file.Description,
            Subject = file.PatientId is { } pid ? new ResourceReference($"Patient/{pid}") : null,
            Date = file.CreatedAt,
            Content =
            [
                new FhirDocumentReference.ContentComponent
                {
                    Attachment = new Attachment
                    {
                        Title = file.FileName,
                        Url = $"/files/{file.Id}/content",
                        Size = (uint)Math.Clamp(file.SizeBytes, 0, uint.MaxValue),
                    },
                },
            ],
        };

    public static Bundle BuildPatientEverythingBundle(
        DomainPatient patient,
        IEnumerable<Visit> visits,
        IEnumerable<StoredFile> files
    )
    {
        var bundle = new Bundle
        {
            Id = $"patient-{patient.Id}-everything",
            Type = Bundle.BundleType.Collection,
            Timestamp = DateTimeOffset.UtcNow,
        };

        AddEntry(bundle, ToFhirPatient(patient));
        if (ToFhirAllergyIntolerance(patient) is { } allergy)
        {
            AddEntry(bundle, allergy);
        }

        foreach (var visit in visits)
        {
            AddEntry(bundle, ToFhirEncounter(visit));
            if (visit.Vitals is not null)
            {
                foreach (var obs in ToFhirVitalsObservations(visit.Vitals, patient.Id))
                {
                    AddEntry(bundle, obs);
                }
            }

            foreach (var consult in visit.Consults)
            {
                foreach (var dx in consult.Diagnoses)
                {
                    AddEntry(bundle, ToFhirCondition(dx, patient.Id, visit.Id));
                }
                foreach (var order in consult.Orders)
                {
                    AddEntry(bundle, ToFhirMedicationRequest(order, patient.Id, visit.Id));
                }
                foreach (var referral in consult.Referrals)
                {
                    AddEntry(bundle, ToFhirServiceRequest(referral, patient.Id, visit.Id));
                }
            }
        }

        foreach (var file in files)
        {
            AddEntry(bundle, ToFhirDocumentReference(file));
        }

        return bundle;
    }

    private static void AddEntry(Bundle bundle, Resource resource) =>
        bundle.Entry.Add(
            new Bundle.EntryComponent
            {
                FullUrl = $"{resource.TypeName}/{resource.Id}",
                Resource = resource,
            }
        );
}
