using System.Data.Common;
using System.Net.Http.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SaBai.API.Domain;
using SaBai.Tests.Support;
using static Qdrant.Client.Grpc.Conditions;

namespace SaBai.Tests.Patients;

/// <summary>
/// Face enrolment needs the patient's current biometric consent, withdrawing it deletes the
/// stored faces, and callers cannot loosen the match threshold. The tests have no photo of a real
/// face, so withdrawal is checked against embeddings written straight to Qdrant.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class FaceConsentTests(AppFixture fixture)
{
    private const string Collection = "faces";
    private const string PersonIdField = "person_id";

    [Test]
    public async Task Enrol_WithoutConsent_ReturnsForbidden()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);

        // Act
        var response = await EnrolAsync(client, patientId);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(body).Contains("consent");
    }

    [Test]
    public async Task Enrol_AfterConsentWithdrawn_ReturnsForbidden()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        await RecordConsentAsync(client, patientId, granted: true);
        await RecordConsentAsync(client, patientId, granted: false);

        // Act
        var response = await EnrolAsync(client, patientId);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Enrol_WithConsent_IsNotRefused()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        await RecordConsentAsync(client, patientId, granted: true);

        // Act
        var response = await EnrolAsync(client, patientId);

        // Assert
        // The sample image has no face, so with the models loaded this is 422; the point is that
        // the consent check let it through to face detection.
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task WithdrawConsent_DeletesThePatientsStoredFaces()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        var otherPatientId = await CreatePatientAsync(client);
        await RecordConsentAsync(client, patientId, granted: true);
        using var qdrant = await CreateQdrantClientAsync();
        await EnsureCollectionAsync(qdrant);
        await StoreFaceAsync(qdrant, $"patient:{patientId}");
        await StoreFaceAsync(qdrant, $"patient:{patientId}");
        await StoreFaceAsync(qdrant, $"patient:{otherPatientId}");

        // Act
        var withdrawn = await RecordConsentAsync(client, patientId, granted: false);
        var remaining = await CountFacesAsync(qdrant, $"patient:{patientId}");
        var otherRemaining = await CountFacesAsync(qdrant, $"patient:{otherPatientId}");

        // Assert
        await Assert.That(withdrawn.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(remaining).IsEqualTo(0UL);
        await Assert.That(otherRemaining).IsEqualTo(1UL);
        await qdrant.DeleteAsync(Collection, MatchKeyword(PersonIdField, $"patient:{otherPatientId}"));
    }

    [Test]
    public async Task GrantConsent_KeepsStoredFaces()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        using var qdrant = await CreateQdrantClientAsync();
        await EnsureCollectionAsync(qdrant);
        await StoreFaceAsync(qdrant, $"patient:{patientId}");

        // Act
        var granted = await RecordConsentAsync(client, patientId, granted: true);
        var remaining = await CountFacesAsync(qdrant, $"patient:{patientId}");

        // Assert
        await Assert.That(granted.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(remaining).IsEqualTo(1UL);
        await qdrant.DeleteAsync(Collection, MatchKeyword(PersonIdField, $"patient:{patientId}"));
    }

    [Test]
    public async Task EnrolPerson_WithPatientIdentity_ReturnsBadRequest()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        using var content = ImageForm("image");

        // Act
        var response = await client.PostAsync($"/persons/patient:{patientId}/faces", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("/patients/search-by-face?threshold=-1")]
    [Arguments("/patients/search-by-face?threshold=0.1")]
    [Arguments("/patients/search-by-face?threshold=1.5")]
    [Arguments("/faces/identify?threshold=-1")]
    [Arguments("/faces/identify?threshold=0.1")]
    public async Task Search_ThresholdOutsideConfiguredToOne_ReturnsBadRequest(string url)
    {
        // Arrange
        var client = await LoginAsync();
        using var content = ImageForm("image");

        // Act
        var response = await client.PostAsync(url, content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Photo_HtmlClaimingJpeg_ReturnsBadRequest()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        using var content = new MultipartFormDataContent();
        content.Add(SampleFiles.Part(SampleFiles.Html, "image/jpeg"), "image", "photo.jpg");

        // Act
        var response = await client.PutAsync($"/patients/{patientId}/photo", content);
        var photo = await client.GetAsync($"/patients/{patientId}/photo");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(photo.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Photo_WithoutConsent_IsStoredAndServedWithItsRealType()
    {
        // Arrange
        var client = await LoginAsync();
        var patientId = await CreatePatientAsync(client);
        using var content = new MultipartFormDataContent();
        // A PNG sent as JPEG named .gif: the stored type follows the bytes.
        content.Add(SampleFiles.Part(SampleFiles.Png(), "image/jpeg"), "image", "photo.gif");

        // Act
        var stored = await client.PutAsync($"/patients/{patientId}/photo", content);
        var photo = await client.GetAsync($"/patients/{patientId}/photo");

        // Assert
        await Assert.That(stored.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(photo.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(photo.Content.Headers.ContentType?.MediaType).IsEqualTo("image/png");
        await Assert.That(photo.Content.Headers.ContentDisposition?.DispositionType).IsEqualTo("attachment");
        await Assert.That(photo.Headers.Contains("X-Content-Type-Options")).IsTrue();
        await Assert.That(photo.Headers.Contains("Content-Security-Policy")).IsTrue();
    }

    private async Task<HttpClient> LoginAsync()
    {
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        return client;
    }

    private static async Task<Guid> CreatePatientAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        return patientId;
    }

    private static async Task<HttpResponseMessage> EnrolAsync(HttpClient client, Guid patientId)
    {
        using var content = ImageForm("image");
        return await client.PostAsync($"/patients/{patientId}/faces", content);
    }

    private static MultipartFormDataContent ImageForm(string field)
    {
        var content = new MultipartFormDataContent();
        content.Add(SampleFiles.Part(SampleFiles.Png(), "image/png"), field, "face.png");
        return content;
    }

    private static async Task<HttpResponseMessage> RecordConsentAsync(
        HttpClient client,
        Guid patientId,
        bool granted
    )
    {
        var response = await client.PostAsJsonAsync(
            $"/patients/{patientId}/consents",
            new { consentType = ConsentType.BiometricFaceRecognition, granted },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task<QdrantClient> CreateQdrantClientAsync()
    {
        var connection = new DbConnectionStringBuilder
        {
            ConnectionString = await fixture.App.GetConnectionStringAsync("qdrant"),
        };
        return new QdrantClient(
            new Uri((string)connection["Endpoint"]),
            apiKey: connection.TryGetValue("Key", out var key) ? (string)key : null
        );
    }

    /// <summary>The API creates the collection once its models load; the tests may get there first.</summary>
    private static async Task EnsureCollectionAsync(QdrantClient qdrant)
    {
        if (await qdrant.CollectionExistsAsync(Collection))
        {
            return;
        }

        try
        {
            await qdrant.CreateCollectionAsync(
                Collection,
                new VectorParams { Size = 512, Distance = Distance.Cosine }
            );
        }
        catch (Grpc.Core.RpcException)
        {
            // Created by the API in the meantime.
            if (!await qdrant.CollectionExistsAsync(Collection))
            {
                throw;
            }
        }
    }

    private static async Task StoreFaceAsync(QdrantClient qdrant, string personId)
    {
        var info = await qdrant.GetCollectionInfoAsync(Collection);
        var size = (int)info.Config.Params.VectorsConfig.Params.Size;
        var embedding = new float[size];
        for (var i = 0; i < size; i++)
        {
            embedding[i] = Random.Shared.NextSingle() - 0.5f;
        }

        await qdrant.UpsertAsync(
            Collection,
            [
                new PointStruct
                {
                    Id = Guid.NewGuid(),
                    Vectors = embedding,
                    Payload =
                    {
                        [PersonIdField] = personId,
                        ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                    },
                },
            ]
        );
    }

    private static Task<ulong> CountFacesAsync(QdrantClient qdrant, string personId) =>
        qdrant.CountAsync(Collection, MatchKeyword(PersonIdField, personId), exact: true);
}
