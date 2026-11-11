import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    // Kotlin version matches the one Spring Boot's BOM manages.
    kotlin("jvm") version "2.3.21"
    // Opens @Component/@Transactional classes so Spring can proxy them (Kotlin classes are final by default).
    kotlin("plugin.spring") version "2.3.21"
    // Adds the no-arg constructors JPA needs on @Entity classes.
    kotlin("plugin.jpa") version "2.3.21"
    id("org.springframework.boot") version "4.1.2"
    id("io.spring.dependency-management") version "1.1.7"
}

group = "sabai"
version = "0.0.1"

java {
    toolchain { languageVersion = JavaLanguageVersion.of(21) }
}

// Hibernate creates lazy proxies by subclassing entities, so they must stay open.
allOpen {
    annotation("jakarta.persistence.Entity")
    annotation("jakarta.persistence.MappedSuperclass")
    annotation("jakarta.persistence.Embeddable")
}

dependencyManagement {
    imports { mavenBom("software.amazon.awssdk:bom:2.57.12") }
}

dependencies {
    // Web + JSON (ASP.NET Core + FastEndpoints)
    implementation("org.springframework.boot:spring-boot-starter-webmvc")
    implementation("org.springframework.boot:spring-boot-starter-validation")
    implementation("tools.jackson.module:jackson-module-kotlin")
    implementation("org.jetbrains.kotlin:kotlin-reflect")
    implementation("org.springdoc:springdoc-openapi-starter-webmvc-ui:3.2.0")

    // Persistence (EF Core + Npgsql + migrations)
    implementation("org.springframework.boot:spring-boot-starter-data-jpa")
    implementation("org.springframework.boot:spring-boot-starter-flyway")
    implementation("org.flywaydb:flyway-database-postgresql")
    implementation("com.linecorp.kotlin-jdsl:jpql-dsl:3.9.0")
    implementation("com.linecorp.kotlin-jdsl:jpql-render:3.9.0")
    implementation("com.linecorp.kotlin-jdsl:spring-data-jpa-boot4-support:3.9.0")
    runtimeOnly("org.postgresql:postgresql")

    // Auth (ASP.NET Core Identity + cookie auth + Data Protection key ring in Postgres)
    implementation("org.springframework.boot:spring-boot-starter-security")
    implementation("org.springframework.boot:spring-boot-starter-session-jdbc")

    // Observability (Aspire ServiceDefaults: health checks + OpenTelemetry)
    implementation("org.springframework.boot:spring-boot-starter-actuator")
    implementation("org.springframework.boot:spring-boot-starter-opentelemetry")

    // Face recognition (Microsoft.ML.OnnxRuntime + SkiaSharp + Qdrant.Client)
    implementation("com.microsoft.onnxruntime:onnxruntime:1.31.1")
    implementation("com.drewnoakes:metadata-extractor:2.22.0")
    implementation("io.qdrant:client:1.20.0")
    implementation("io.grpc:grpc-netty-shaded")
    implementation("com.google.guava:guava:33.5.0-jre")

    // Files + PDF (AWSSDK.S3 + QuestPDF)
    implementation("software.amazon.awssdk:s3")
    implementation("com.github.librepdf:openpdf:3.0.5")

    // Tests (TUnit + Aspire.Hosting.Testing)
    testImplementation("org.springframework.boot:spring-boot-starter-test")
    testImplementation("org.springframework.boot:spring-boot-starter-webmvc-test")
    testImplementation("org.springframework.boot:spring-boot-starter-security-test")
    testImplementation("org.springframework.boot:spring-boot-testcontainers")
    testImplementation("org.testcontainers:testcontainers-postgresql")
    testImplementation("org.testcontainers:testcontainers-junit-jupiter")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

kotlin {
    compilerOptions {
        jvmTarget = JvmTarget.JVM_21
        // Treat Java nullability annotations (Spring uses JSpecify) as real Kotlin types.
        freeCompilerArgs.addAll("-Xjsr305=strict", "-Xannotation-default-target=param-property")
    }
}

tasks.withType<Test> {
    useJUnitPlatform()
}
