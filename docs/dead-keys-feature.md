# Tính năng tự động lưu Dead Keys

## Tổng quan

Tính năng này tự động lưu các API key của ElevenLabs bị exhausted (hết quota hoặc lỗi) vào file `DeadKeys.txt` trong thư mục `Configs`.

## Các thay đổi được thực hiện

### 1. ApiKeyManager.cs

- **Phương thức mới**: `SaveExhaustedKeyToFile(ApiKeyInfo keyInfo, string reason)`

  - Tự động lưu thông tin key bị exhausted vào file `DeadKeys.txt`
  - Ghi log bao gồm: timestamp, key, lý do (status code/reason), số lần sử dụng, thời gian cooldown

- **Cập nhật**: `MarkKeyExhausted(string key, TimeSpan? cooldownPeriod = null)`
  - Thêm lời gọi `SaveExhaustedKeyToFile()` khi key bị đánh dấu exhausted
- **Cập nhật**: `MarkKeyExhausted(string key, HttpResponseMessage response)`
  - Thêm lời gọi `SaveExhaustedKeyToFile()` với thông tin status code từ response
  - Tự động parse X-RateLimit-Reset header để xác định thời gian cooldown chính xác

### 2. ElevenlabVoiceControlViewModel.cs

- **Command mới**: `ViewDeadKeysCommand`

  - Cho phép người dùng xem file DeadKeys.txt
  - Hiển thị thông tin tổng quan về số lượng dead keys
  - Mở file bằng Notepad khi người dùng chọn xem chi tiết

- **Phương thức mới**: `ViewDeadKeys()`

  - Kiểm tra sự tồn tại của file DeadKeys.txt
  - Hiển thị thống kê số lượng dead keys
  - Cho phép mở file bằng Notepad để xem chi tiết

- **Cải thiện logging**:
  - Thêm log khi key được lưu vào DeadKeys.txt
  - Log message: `[DEAD_KEY_SAVED] Key đã được lưu vào file DeadKeys.txt trong thư mục Configs`

## Format file DeadKeys.txt

```
[2025-11-15 11:30:45] Key: sk-xxx...xxx | Reason: TooManyRequests | UsedCount: 150 (Cooldown until: 2025-11-15 12:30:45)
[2025-11-15 11:35:20] Key: sk-yyy...yyy | Reason: quota_exceeded | UsedCount: 200 (Cooldown until: 2025-11-15 13:00:00)
[2025-11-15 11:40:15] Key: sk-zzz...zzz | Reason: invalid_api_key | UsedCount: 50
```

Mỗi dòng bao gồm:

- **Timestamp**: Thời gian key bị đánh dấu exhausted
- **Key**: API key (có thể là partial key để bảo mật)
- **Reason**: Lý do (status code hoặc error message)
- **UsedCount**: Số lần key đã được sử dụng
- **Cooldown until**: Thời gian key sẽ được reset (nếu có)

## Khi nào key được lưu vào DeadKeys.txt?

Key sẽ được tự động lưu khi gặp các trường hợp sau:

1. **Status Code 429** (TooManyRequests)
2. **Error message chứa**:
   - "quota exceeded"
   - "quota_exceeded"
   - "exceeds"
   - "rate limit"
   - "invalid_api_key"
   - "detected_unusual_activity"

## Cách sử dụng

### Xem Dead Keys qua UI

1. Trong ứng dụng, sử dụng button/command `ViewDeadKeys`
2. Một dialog sẽ hiển thị số lượng dead keys
3. Chọn "Yes" để mở file bằng Notepad

### Xem Dead Keys trực tiếp

- File được lưu tại: `Configs/DeadKeys.txt`
- Có thể mở bằng bất kỳ text editor nào

## Lợi ích

1. **Theo dõi tự động**: Không cần theo dõi thủ công key nào bị lỗi
2. **Lịch sử đầy đủ**: Lưu trữ toàn bộ lịch sử các key bị exhausted
3. **Debug dễ dàng**: Dễ dàng xác định pattern lỗi và key có vấn đề
4. **Audit trail**: Có bằng chứng về thời điểm và lý do key bị disabled
5. **Không gián đoạn**: Việc lưu file không ảnh hưởng đến luồng xử lý chính

## Lưu ý

- File `DeadKeys.txt` sẽ được append (không ghi đè), nên lịch sử được giữ lại hoàn toàn
- Nếu có lỗi khi ghi file, ứng dụng sẽ tiếp tục hoạt động bình thường (không throw exception)
- File được tạo tự động trong thư mục `Configs` khi có key đầu tiên bị exhausted
- Có thể xóa file để reset lịch sử nếu cần

## Ví dụ sử dụng trong code

```csharp
// Khi phát hiện key bị exhausted trong quá trình xử lý
if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
    contentErr.Contains("quota exceeded"))
{
    // Tự động mark key exhausted và lưu vào DeadKeys.txt
    _apiKeyManager.MarkKeyExhausted(apiKeyInfo.Key, response);
    Log($"[KEY_EXHAUSTED] Key {apiKeyInfo.Key} đã bị khóa");
    Log($"[DEAD_KEY_SAVED] Key đã được lưu vào file DeadKeys.txt");
}
```

## Tương lai

Có thể mở rộng tính năng:

- Thêm command để export dead keys ra format khác (CSV, JSON)
- Tự động cảnh báo khi số lượng dead keys vượt ngưỡng
- Tích hợp với dashboard/monitoring system
- Tự động gửi email/notification khi có key mới bị exhausted
