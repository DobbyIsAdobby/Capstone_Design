using System;
using System.IO;
using System.Security.Cryptography;     // .NET 환경에서 데이터 암호화, 복호화, 해싱, 디지털 서명 및 난수 생성을 하기 위한 nameSpace
using System.Text;
using UnityEngine;

/// <summary>
/// 슬롯 파일을 조회했을 때의 상태. 
/// 파일이 없는 것과 손상된 것은 구분
/// </summary>
public enum SaveSlotStatus
{
    Empty,      // 파일 자체가 없음
    Ready,      // 파일 읽기와 검증에 성공
    Invalid     // 파일은 있지만 읽거나 사용할 수 없음
}

public static class SaveFileService
{
    /// <summary>
    /// 실제 파일에 기록할 바깥쪽 데이터. 
    /// 
    /// payload: 
    /// GameSaveData를 JSON 문자열로 변환한 내용. 
    /// 
    /// checksum: 
    /// payload가 기록 당시와 같은지 확인하는 검사값. 
    /// 
    /// 암호화나 부정행위 방지 용도는 아님
    /// </summary>
    [Serializable]
    private sealed class Envelope
    {
        public string payload;
        public string checksum;
    }

    /// <summary>
    /// 기기별 게임 데이터 폴더 아래의 Saves 경로. 
    /// 이 속성은 경로만 계산하며 폴더를 만들지는 않음
    /// </summary>
    private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "Saves");

    /// <summary>
    /// 슬롯 번호를 실제 파일 경로로 변환. 
    /// ex) 1 → .../Saves/slot_1.json 
    /// 슬롯 번호는 유저 화면과 동일하게 1~3을 사용
    /// </summary>
    /// <param name="slot"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetSlotPath(int slot)
    {
        if (slot < 1 || slot > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "슬롯 번호는 1~3이어야 합니다.");
        }

        return Path.Combine(DirectoryPath, $"slot_{slot}.json");
    }

    /// <summary>
    /// 해당 슬롯 경로에 파일이 존재하는지만 검사.
    /// true여도 정상 저장 파일이라는 뜻은 아님.
    /// 정상 여부는 TryRead에서 확인.
    /// </summary>
    /// <param name="slot"></param>
    /// <returns></returns>
    public static bool Exists(int slot)
    {
        return File.Exists(GetSlotPath(slot));
    }

    /// <summary>
    /// 슬롯 목록을 표시할 때 사용하는 조회 메서드.
    /// 
    /// 반환값:
    /// Empty   → 파일 없음
    /// Ready   → 정상 파일
    /// Invalid → 손상 또는 호환되지 않는 파일
    ///
    /// out data:
    /// 정상 파일이면 읽어낸 게임 데이터를 전달.
    ///
    /// out error:
    /// 실패한 경우 원인 설명을 전달.
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="config"></param>
    /// <param name="data"></param>
    /// <param name="error"></param>
    /// <returns></returns> 
    public static SaveSlotStatus Inspect(int slot, SaveValidationConfig config, out GameSaveData data, out string error)
    {
        data = null;
        error = "";

        // 파일 자체가 없으면 내용을 읽을 필요가 없음
        if (!Exists(slot))
            return SaveSlotStatus.Empty;

        // 파일이 있으면 내용까지 읽고 검증.
        bool success = TryRead(slot, config, out data, out error);

        return success ? SaveSlotStatus.Ready : SaveSlotStatus.Invalid;
    }

    /// <summary>
    /// 슬롯 파일을 읽고 성공 여부를 반환.
    /// 
    /// 성공:
    /// true 반환, data에 저장 데이터 전달.
    ///
    /// 실패:
    /// false 반환, error에 실패 원인 전달.
    ///
    /// 읽기 중 발생하는 예외를 이곳에서 처리함.
    /// UI에서는 반환값을 보고 안내를 표시하면 됨.
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="config"></param>
    /// <param name="data"></param>
    /// <param name="error"></param>
    /// <returns></returns> 
    public static bool TryRead(int slot, SaveValidationConfig config, out GameSaveData data, out string error)
    {
        data = null;
        error = "";

        try
        {
            string path = GetSlotPath(slot);

            // 파일 읽기와 내용 검증은 ReadPath가 담당.
            data = ReadPath(path, config);

            return true;
        }
        catch (Exception exception)
        {
            error = $"저장 데이터를 읽지 못했습니다.\n{exception.Message}";

            return false;
        }
    }

    /// <summary>
    /// 게임 데이터를 지정한 슬롯에 기록.
    ///
    /// allowOverwrite:
    /// false → 기존 파일이 있으면 저장 거부.
    /// true  → 유저가 확인한 것으로 보고 덮어쓰기를 허용.
    ///
    /// 기존 파일부터 삭제하지 않음.
    /// 임시 파일을 기록하고 검증한 다음 마지막에 교체.
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="data"></param>
    /// <param name="config"></param>
    /// <param name="allowOverwrite"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool TryWrite(int slot, GameSaveData data, SaveValidationConfig config, bool allowOverwrite, out string error)
    {
        error = "";

        // 실패했을 때 정리할 임시 파일 경로.
        string temporaryPath = null;

        try
        {
            if (config == null)
            {
                throw new InvalidOperationException("저장 검증 설정이 없습니다.");
            }

            // 잘못된 게임 상태를 파일로 기록하지 않도록 먼저 검사.
            config.Validate(data);

            string targetPath = GetSlotPath(slot);

            // 덮어쓰기 확인 없이 기존 슬롯을 변경하지 않음.
            if (File.Exists(targetPath) && !allowOverwrite)
            {
                throw new IOException("기존 파일 덮어쓰기 확인이 필요합니다.");
            }

            // 폴더가 없으면 만들고, 이미 있으면 그대로 사용.
            Directory.CreateDirectory(DirectoryPath);

            // 게임 데이터를 JSON 문자열로 변환.
            string payload = JsonUtility.ToJson(data);

            // 게임 데이터와 손상 검사값을 함께 보관.
            var envelope = new Envelope
            {
                payload = payload,
                checksum = Hash(payload)
            };

            // 같은 폴더 안에 서로 겹치지 않는 임시 파일 생성
            temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            File.WriteAllText(temporaryPath, JsonUtility.ToJson(envelope, true), new UTF8Encoding(false));

            // 메모리 데이터만 검사하는 것이 아니라, 기록된 임시 파일을 다시 읽어 검증.
            ReadPath(temporaryPath, config);

            if (File.Exists(targetPath))
            {
                // 저장 준비 중 파일이 새로 생긴 경우도 다시 검사.
                if (!allowOverwrite)
                {
                    throw new IOException("슬롯에 저장 파일이 생겼습니다. 다시 확인해주세요.");
                }

                // 기존 슬롯을 검증된 임시 파일로 교체.
                // 교체 실패 시 기존 파일을 삭제하는 방식으로 우회하지 않음.
                File.Replace(temporaryPath, targetPath, null);
            }
            else
            {
                // 빈 슬롯이면 임시 파일을 정식 이름으로 이동.
                File.Move(temporaryPath, targetPath);
            }

            // 성공했으니 finally에서 삭제할 임시 파일은 없음.
            temporaryPath = null;

            return true;
        }
        catch (Exception exception)
        {
            error = $"저장하지 못했습니다.\n{exception.Message}";
            return false;
        }
        finally
        {
            // 성공, 실패와 관계없이 실행되는 정리 구간.
            // 실패한 임시 파일만 삭제하며 기존 슬롯 파일은 건드리지 않음
            if (temporaryPath != null)
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception exception)
                {
                    // 임시 파일 정리 실패가 원래 오류를 덮지 않게 함.
                    Debug.LogWarning(exception.Message);
                }
            }
        }
    }

    /// <summary>
    /// 파일 경로를 받아 내용을 읽고 검증.
    /// 
    /// 1. 파일과 설정 확인
    /// 2. JSON을 Envelope로 변환
    /// 3. 검사값 비교
    /// 4. payload를 GameSaveData로 변환
    /// 5. 게임 규칙에 맞는지 검사
    ///
    /// 문제가 있으면 예외를 발생시키고,
    /// TryRead 또는 TryWrite가 이를 처리.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="FormatException"></exception>
    private static GameSaveData ReadPath(string path, SaveValidationConfig config)
    {
        if (config == null)
        {
            throw new InvalidOperationException("저장 검증 설정이 없습니다.");
        }

        if (!File.Exists(path))
            throw new FileNotFoundException("저장 파일이 없습니다.");

        // 잘못된 대용량 파일을 읽는 상황을 제한.
        if (new FileInfo(path).Length > 8 * 1024 * 1024)
            throw new FormatException("저장 파일 크기가 너무 큽니다.");

        string json = File.ReadAllText(path, Encoding.UTF8);

        Envelope envelope = JsonUtility.FromJson<Envelope>(json);

        // payload로 검사값을 다시 계산하여 기록 당시 값과 비교.
        if (envelope == null ||
            string.IsNullOrEmpty(envelope.payload) ||
            !string.Equals(
                envelope.checksum,
                Hash(envelope.payload),
                StringComparison.Ordinal))
        {
            throw new FormatException("저장 파일 내용이 손상되었습니다.");
        }

        GameSaveData data = JsonUtility.FromJson<GameSaveData>(envelope.payload);

        // 파일 내용이 온전해도 현재 게임 규칙과 맞지 않을 수 있음.
        config.Validate(data);

        return data;
    }

    /// <summary>
    /// 문자열의 SHA-256 검사값을 계산.
    /// 같은 문자열이면 같은 결과가 나옴.
    ///
    /// 파일 암호화가 아니라 내용 손상 검사에 사용.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static string Hash(string text)
    {
        // using을 사용해 계산이 끝난 자원을 정리.
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes =
                sha.ComputeHash(Encoding.UTF8.GetBytes(text));

            // byte 배열을 비교하기 쉬운 문자열로 변환.
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
