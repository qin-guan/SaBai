//! Makes sure the ONNX model files exist locally, downloading the model pack if allowed.

use std::fs::{self, File};
use std::io;
use std::path::{Path, PathBuf};

use anyhow::{Context, anyhow, bail};
use tokio::io::AsyncWriteExt;
use uuid::Uuid;

use crate::config::FaceRecognitionSettings;

pub async fn ensure_models(options: &FaceRecognitionSettings) -> anyhow::Result<()> {
    let required = [options.detector_path(), options.recognizer_path()];
    let missing: Vec<PathBuf> = required.into_iter().filter(|path| !path.is_file()).collect();
    if missing.is_empty() {
        return Ok(());
    }

    if !options.auto_download_models {
        bail!(
            "Face recognition model(s) not found: {}. Place them in '{}' or enable FaceRecognition:AutoDownloadModels.",
            missing.iter().map(|path| path.display().to_string()).collect::<Vec<_>>().join(", "),
            options.models_directory.display()
        );
    }

    tokio::fs::create_dir_all(&options.models_directory).await?;
    let archive = options.models_directory.join(format!("download-{}.zip", Uuid::new_v4().simple()));
    let result = download_and_extract(&options.model_pack_url, &archive, missing).await;
    let _ = tokio::fs::remove_file(&archive).await;
    result
}

async fn download_and_extract(url: &str, archive: &Path, missing: Vec<PathBuf>) -> anyhow::Result<()> {
    tracing::info!("Downloading face recognition models from {url}");
    // No overall timeout: the pack is a few hundred megabytes.
    let mut response = reqwest::Client::new().get(url).send().await?.error_for_status()?;
    let mut file = tokio::fs::File::create(archive).await?;
    while let Some(chunk) = response.chunk().await? {
        file.write_all(&chunk).await?;
    }
    file.flush().await?;
    drop(file);

    let (archive, source) = (archive.to_owned(), url.to_owned());
    tokio::task::spawn_blocking(move || extract(&archive, &missing, &source)).await?
}

/// Copies each missing model out of the archive, matching entries by file name in any
/// case and folder. A model appears under its final name only once it is complete.
fn extract(archive: &Path, missing: &[PathBuf], source: &str) -> anyhow::Result<()> {
    let mut zip = zip::ZipArchive::new(File::open(archive)?)?;
    for path in missing {
        let name = path
            .file_name()
            .and_then(|name| name.to_str())
            .ok_or_else(|| anyhow!("Model path '{}' has no file name.", path.display()))?;
        let index = (0..zip.len())
            .find(|&index| {
                zip.by_index(index).is_ok_and(|entry| {
                    !entry.is_dir() && entry.name().rsplit(['/', '\\']).next().is_some_and(|n| n.eq_ignore_ascii_case(name))
                })
            })
            .ok_or_else(|| anyhow!("Model '{name}' not found in {source}."))?;

        let mut temp = path.clone().into_os_string();
        temp.push(".tmp");
        let temp = PathBuf::from(temp);
        io::copy(&mut zip.by_index(index)?, &mut File::create(&temp)?)
            .with_context(|| format!("Could not extract face model '{name}'."))?;
        fs::rename(&temp, path)?;
        tracing::info!("Extracted face model {}", path.display());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use std::io::Write;

    use super::*;

    fn scratch() -> PathBuf {
        let directory = std::env::temp_dir().join(format!("sabai-models-{}", Uuid::new_v4().simple()));
        fs::create_dir_all(&directory).unwrap();
        directory
    }

    fn options(directory: &Path, auto_download: bool) -> FaceRecognitionSettings {
        FaceRecognitionSettings {
            models_directory: directory.to_owned(),
            auto_download_models: auto_download,
            ..FaceRecognitionSettings::default()
        }
    }

    #[tokio::test]
    async fn missing_models_are_an_error_when_downloads_are_off() {
        let directory = scratch();
        fs::write(directory.join("det_10g.onnx"), b"detector").unwrap();

        let error = ensure_models(&options(&directory, false)).await.unwrap_err().to_string();

        assert_eq!(
            error,
            format!(
                "Face recognition model(s) not found: {}. Place them in '{}' or enable FaceRecognition:AutoDownloadModels.",
                directory.join("w600k_r50.onnx").display(),
                directory.display()
            )
        );
        fs::remove_dir_all(directory).unwrap();
    }

    #[tokio::test]
    async fn present_models_need_nothing() {
        let directory = scratch();
        fs::write(directory.join("det_10g.onnx"), b"detector").unwrap();
        fs::write(directory.join("w600k_r50.onnx"), b"recognizer").unwrap();

        ensure_models(&options(&directory, false)).await.unwrap();
        fs::remove_dir_all(directory).unwrap();
    }

    #[test]
    fn extracts_models_by_file_name_from_any_folder() {
        let directory = scratch();
        let archive = directory.join("pack.zip");
        let mut writer = zip::ZipWriter::new(File::create(&archive).unwrap());
        let entry = zip::write::SimpleFileOptions::default();
        writer.start_file("buffalo_l/DET_10G.onnx", entry).unwrap();
        writer.write_all(b"detector").unwrap();
        writer.start_file("other.onnx", entry).unwrap();
        writer.write_all(b"other").unwrap();
        writer.finish().unwrap();

        let detector = directory.join("det_10g.onnx");
        extract(&archive, std::slice::from_ref(&detector), "the pack").unwrap();
        assert_eq!(fs::read(&detector).unwrap(), b"detector");
        assert!(!directory.join("det_10g.onnx.tmp").exists());

        let error = extract(&archive, &[directory.join("w600k_r50.onnx")], "the pack").unwrap_err().to_string();
        assert_eq!(error, "Model 'w600k_r50.onnx' not found in the pack.");
        fs::remove_dir_all(directory).unwrap();
    }
}
