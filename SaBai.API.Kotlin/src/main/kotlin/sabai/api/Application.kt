package sabai.api

import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.context.properties.ConfigurationPropertiesScan
import org.springframework.boot.runApplication
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.FullyQualifiedAnnotationBeanNameGenerator
import java.time.Clock

/**
 * Program.cs. Most of what Program.cs wires by hand is auto-configured by Spring Boot
 * from the dependencies and application.yaml; the rest lives in @Configuration classes
 * next to the code they configure (auth/SecurityConfig, facerecognition/FaceRecognitionConfig...).
 *
 * Every endpoint class is called `Endpoint` (one package per endpoint, like the C#
 * folders), so beans are named by their fully qualified class name.
 */
@SpringBootApplication(nameGenerator = FullyQualifiedAnnotationBeanNameGenerator::class)
@ConfigurationPropertiesScan
class Application {
    /** TimeProvider.System. Tests can swap in a fixed clock. */
    @Bean
    fun clock(): Clock = Clock.systemUTC()
}

fun main(args: Array<String>) {
    runApplication<Application>(*args)
}
