using ZXing.Net.Maui;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace BarCodeScanner;

public partial class MainPage : ContentPage
{
    string lastDetectedBarcode = string.Empty;
    DateTime lastDetectionTime = DateTime.MinValue;
    private bool isFlashOn = false;

    public MainPage()
    {
        InitializeComponent();

        cameraBarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats =
                BarcodeFormat.Code128 |
                BarcodeFormat.Code39 |
                BarcodeFormat.Code93 |
                BarcodeFormat.Ean13 |
                BarcodeFormat.Ean8 |
                BarcodeFormat.UpcA |
                BarcodeFormat.UpcE |
                BarcodeFormat.Itf |
                BarcodeFormat.Codabar |
                BarcodeFormat.DataMatrix |
                BarcodeFormat.QrCode |
                BarcodeFormat.Pdf417 |
                BarcodeFormat.Aztec,

            AutoRotate = true,
            Multiple = false,
            TryHarder = true,
            TryInverted = true
        };
    }

    private void OnFlashButtonClicked(object sender, EventArgs e)
    {
        isFlashOn = !isFlashOn;
        cameraBarcodeReaderView.IsTorchOn = isFlashOn;
        
        if (sender is Button button)
        {
            button.Text = isFlashOn ? "🔆" : "🔦";
        }
    }

    protected void BarcodeDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var first = e.Results.FirstOrDefault();
        if (first is null) return;

        string rawValue = first.Value;
        string type = first.Format.ToString();

        Debug.WriteLine($"Detected barcode length: {rawValue.Length}");
        Debug.WriteLine($"Raw value: {rawValue}");

        // Chống quét trùng trong 2 giây
        if (rawValue == lastDetectedBarcode && (DateTime.Now - lastDetectionTime).TotalSeconds < 2)
            return;

        lastDetectedBarcode = rawValue;
        lastDetectionTime = DateTime.Now;

        string displayInfo = "";
        if (type == "Code128")
        {
            Debug.WriteLine($"Processing Code128 with length: {rawValue.Length}");
            LogRawBytes(rawValue); // Debug bytes

            var parsed = ParseGs1Data(rawValue);

            if (parsed.Any())
            {
                displayInfo = "=== THÔNG TIN GS1-128 ===\n";
                foreach (var kv in parsed)
                {
                    string description = GetAiDescription(kv.Key);
                    displayInfo += $"{description}\nAI ({kv.Key}): {kv.Value}\n\n";
                }
            }
            else
            {
                displayInfo = $"=== CODE128 RAW ===\nĐộ dài: {rawValue.Length}\n";
                displayInfo += $"Giá trị thô: {rawValue}\n\n";
                displayInfo += "Không nhận dạng được cấu trúc GS1-128";
            }
        }
        else
        {
            displayInfo = $"Loại mã: {type}\nGiá trị: {rawValue}";
        }

        Dispatcher.DispatchAsync(async () =>
        {
            string title = type == "Code128" ? "GS1-128 Barcode" : "Barcode";
            
            // Nếu là barcode dài, hiển thị với scroll
            if (displayInfo.Length > 500)
            {
                await DisplayAlert(title, "Barcode dài đã được quét. Xem chi tiết...", "OK");
                await ShowDetailedInfo(title, displayInfo);
            }
            else
            {
                await DisplayAlert(title, displayInfo, "OK");
            }
        });
    }

    private async Task ShowDetailedInfo(string title, string content)
    {
        // Tạo popup chi tiết cho barcode dài
        await DisplayAlert(title, content, "Đóng");
        
        // Có thể thêm copy to clipboard
        try
        {
            await Clipboard.Default.SetTextAsync(content);
            await DisplayAlert("Thông báo", "Đã copy thông tin vào clipboard", "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Clipboard error: {ex.Message}");
        }
    }

    private void LogRawBytes(string data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Raw bytes analysis:");
        for (int i = 0; i < Math.Min(data.Length, 100); i++) // Chỉ log 100 ký tự đầu
        {
            char c = data[i];
            if (c == (char)29) // FNC1
            {
                sb.Append("[FNC1]");
            }
            else if (char.IsControl(c))
            {
                sb.Append($"[{(int)c}]");
            }
            else
            {
                sb.Append(c);
            }
        }
        if (data.Length > 100) sb.Append("...");
        Debug.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Phân tích chuỗi barcode GS1-128 được cải thiện cho barcode dài
    /// </summary>
    public Dictionary<string, string> ParseGs1Data(string raw)
    {
        var result = new Dictionary<string, string>();
        
        if (string.IsNullOrEmpty(raw))
            return result;

        Debug.WriteLine($"=== Parsing GS1 data, length: {raw.Length} ===");
        
        // 1. Kiểm tra cấu trúc dấu ngoặc đơn trước
        if (raw.Contains("(") && raw.Contains(")"))
        {
            return ParseWithParentheses(raw);
        }

        // 2. Phân tích theo FNC1 hoặc cấu trúc liền mạch
        return ParseWithoutParentheses(raw);
    }

    private Dictionary<string, string> ParseWithParentheses(string raw)
    {
        var result = new Dictionary<string, string>();
        
        // Regex cải thiện cho dấu ngoặc đơn - không giới hạn độ dài dữ liệu
        var regex = new Regex(@"\((\d{2,4})\)([^(]*?)(?=\(\d|$)", RegexOptions.Singleline);
        var matches = regex.Matches(raw);
        
        Debug.WriteLine($"Found {matches.Count} parentheses matches");
        
        foreach (Match match in matches)
        {
            string ai = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();
            
            // Loại bỏ ký tự FNC1 nếu có
            value = value.Replace(((char)29).ToString(), "");
            
            Debug.WriteLine($"Parentheses: AI={ai}, Value={value} (length: {value.Length})");
            result[ai] = value;
        }
        
        return result;
    }

    private Dictionary<string, string> ParseWithoutParentheses(string raw)
    {
        var result = new Dictionary<string, string>();
        string processedRaw = raw;
        
        // Loại bỏ ký tự FNC1 đầu tiên nếu có
        if (processedRaw.Length > 0 && processedRaw[0] == (char)29)
        {
            processedRaw = processedRaw.Substring(1);
        }

        Debug.WriteLine($"Processing without parentheses, length: {processedRaw.Length}");
        
        int currentIndex = 0;
        int iterationCount = 0;
        const int maxIterations = 50; // Giới hạn để tránh vòng lặp vô tận

        while (currentIndex < processedRaw.Length && iterationCount < maxIterations)
        {
            iterationCount++;
            string currentAI = "";
            bool foundAI = false;
            
            Debug.WriteLine($"Iteration {iterationCount}: Position {currentIndex}, Remaining: '{processedRaw.Substring(currentIndex, Math.Min(20, processedRaw.Length - currentIndex))}...'");

            // Tìm AI hợp lệ, ưu tiên AI dài hơn trước
            for (int aiLength = 4; aiLength >= 2; aiLength--)
            {
                if (currentIndex + aiLength <= processedRaw.Length)
                {
                    string potentialAI = processedRaw.Substring(currentIndex, aiLength);
                    if (IsKnownAI(potentialAI))
                    {
                        currentAI = potentialAI;
                        currentIndex += aiLength;
                        foundAI = true;
                        Debug.WriteLine($"Found AI: {currentAI}");
                        break;
                    }
                }
            }

            if (!foundAI)
            {
                Debug.WriteLine($"No valid AI found at position {currentIndex}");
                // Thử bỏ qua 1 ký tự và tiếp tục (có thể có ký tự lạ)
                currentIndex++;
                continue;
            }

            // Lấy dữ liệu cho AI
            string currentValue = ExtractAIValue(processedRaw, currentIndex, currentAI);
            int valueLength = currentValue.Length;
            
            // Cập nhật vị trí
            currentIndex += valueLength;
            
            // Bỏ qua ký tự FNC1 nếu có
            if (currentIndex < processedRaw.Length && processedRaw[currentIndex] == (char)29)
            {
                currentIndex++;
            }
            
            result[currentAI] = currentValue;
            Debug.WriteLine($"AI {currentAI}: '{currentValue}' (length: {valueLength})");
        }
        
        if (iterationCount >= maxIterations)
        {
            Debug.WriteLine("Warning: Maximum iterations reached");
        }
        
        return result;
    }

    private string ExtractAIValue(string data, int startIndex, string ai)
    {
        int dataLength = GetAiDataLength(ai);
        
        if (dataLength > 0) // Độ dài cố định
        {
            int availableLength = data.Length - startIndex;
            int actualLength = Math.Min(dataLength, availableLength);
            
            if (actualLength < dataLength)
            {
                Debug.WriteLine($"Warning: Expected {dataLength} chars for AI {ai}, but only {actualLength} available");
            }
            
            return data.Substring(startIndex, actualLength);
        }
        else // Độ dài biến đổi
        {
            // Tìm FNC1 tiếp theo hoặc cuối chuỗi
            int fnc1Index = data.IndexOf((char)29, startIndex);
            if (fnc1Index != -1)
            {
                return data.Substring(startIndex, fnc1Index - startIndex);
            }
            else
            {
                return data.Substring(startIndex);
            }
        }
    }

    private bool IsKnownAI(string ai)
    {
        // Mở rộng danh sách AI và thêm logic cho AI pattern
        if (ai.Length >= 3)
        {
            // Kiểm tra pattern cho AI dạng 3xx
            string prefix = ai.Substring(0, 2);
            if (prefix == "31" || prefix == "32" || prefix == "33" || 
                prefix == "34" || prefix == "35" || prefix == "36" || prefix == "37")
            {
                return true; // AI đo lường
            }
        }
        
        return ai switch
        {
            "00" => true, // SSCC
            "01" => true, // GTIN
            "02" => true, // GTIN của mặt hàng chứa bên trong
            "10" => true, // Batch/Lot Number
            "11" => true, // Production Date (YYMMDD)
            "12" => true, // Due Date (YYMMDD)
            "13" => true, // Packaging Date (YYMMDD)
            "15" => true, // Primary Production Date (YYMMDD)
            "16" => true, // Sell By Date (YYMMDD)
            "17" => true, // Expiration Date (YYMMDD)
            "20" => true, // Product Variant
            "21" => true, // Serial Number
            "22" => true, // HIBCC
            "30" => true, // Quantity (variable)
            "37" => true, // Count of Trade Items (variable)
            "240" => true, // Additional Product Identification
            "241" => true, // Customer Part Number
            "250" => true, // Secondary Serial Number
            "251" => true, // Reference to Source Entity
            "253" => true, // Global Document Type Identifier (GDTI)
            "254" => true, // Global Individual Asset Identifier (GIAI)
            "400" => true, // Customer Order Number
            "401" => true, // Global Location Number (GLN) - Receiver
            "402" => true, // Global Location Number (GLN) - Shipper
            "403" => true, // Global Location Number (GLN) - Bill-to
            "410" => true, // Global Location Number (GLN) - Ship-to
            "411" => true, // Global Location Number (GLN) - Physical Location
            "412" => true, // GLN - Invoicee
            "413" => true, // GLN - Balance
            "414" => true, // GLN - Current
            "420" => true, // Country of Origin
            "421" => true, // Country of Origin (with check digit)
            "422" => true, // Country of Processing
            "423" => true, // Country of Initial Processing
            "424" => true, // Country of Disassembly
            "7001" => true, // Production Date and Time
            "7002" => true, // Expiration Date and Time
            "7003" => true, // Packaging Date and Time
            "7004" => true, // Best Before Date and Time
            "8001" => true, // Roll Products - Product Code
            "8002" => true, // Roll Products - Serial Number
            "8003" => true, // Global Document Type Identifier (GDTI)
            "8004" => true, // Global Individual Asset Identifier (GIAI)
            "8005" => true, // Price Per Unit
            "8006" => true, // Component ID
            "8017" => true, // Best Before Date and Time
            "8018" => true, // Sell By Date and Time
            "8110" => true, // Loyalty points (variable)
            "90" => true, // Mutually Agreed Information
            "91" => true, // Company Internal Information
            "92" => true, // Company Internal Information
            "93" => true, // Company Internal Information
            "94" => true, // Company Internal Information
            "95" => true, // Company Internal Information
            "96" => true, // Company Internal Information
            "97" => true, // Company Internal Information
            "98" => true, // Company Internal Information
            "99" => true, // Company Internal Information
            _ => false
        };
    }

    private int GetAiDataLength(string ai)
    {
        // Cải thiện logic xác định độ dài
        if (ai.StartsWith("31") || ai.StartsWith("32") || ai.StartsWith("33") ||
            ai.StartsWith("34") || ai.StartsWith("35") || ai.StartsWith("36"))
        {
            return 6; // AI đo lường thường có 6 ký tự
        }
        
        return ai switch
        {
            "00" => 18,
            "01" => 14,
            "02" => 14,
            "11" => 6,
            "12" => 6,
            "13" => 6,
            "15" => 6,
            "16" => 6,
            "17" => 6,
            "410" => 13,
            "411" => 13,
            "412" => 13,
            "413" => 13,
            "414" => 13,
            "420" => 3,
            "421" => 3,
            "422" => 3,
            "423" => 3,
            "424" => 3,
            _ => 0 // Độ dài biến đổi
        };
    }

    public string GetAiDescription(string ai)
    {
        // Thêm mô tả cho AI pattern
        if (ai.Length >= 3 && (ai.StartsWith("31") || ai.StartsWith("32") || ai.StartsWith("33")))
        {
            return $"Thông tin đo lường (AI {ai})";
        }
        
        return ai switch
        {
            "00" => "Serial Shipping Container Code (SSCC)",
            "01" => "Global Trade Item Number (GTIN)",
            "02" => "GTIN của mặt hàng thương mại chứa bên trong",
            "10" => "Số Lô / Lô Hàng",
            "11" => "Ngày sản xuất (YYMMDD)",
            "12" => "Ngày đến hạn (YYMMDD)",
            "13" => "Ngày đóng gói (YYMMDD)",
            "15" => "Ngày sản xuất chính (YYMMDD)",
            "16" => "Ngày bán cuối cùng (YYMMDD)",
            "17" => "Ngày hết hạn (YYMMDD)",
            "20" => "Biến thể sản phẩm",
            "21" => "Số Serial",
            "22" => "Dữ liệu ngành dược phẩm (HIBCC)",
            "30" => "Số lượng mặt hàng",
            "37" => "Số lượng mặt hàng thương mại",
            "240" => "Nhận dạng sản phẩm bổ sung",
            "241" => "Số phần của khách hàng",
            "250" => "Số serial thứ cấp",
            "251" => "Tham chiếu đến thực thể nguồn",
            "253" => "Quản lý tài liệu toàn cầu (GDTI)",
            "254" => "Định danh tài sản toàn cầu (GIAI)",
            "400" => "Mã đơn hàng khách hàng",
            "401" => "GLN - Người nhận",
            "402" => "GLN - Người gửi",
            "403" => "GLN - Thanh toán",
            "410" => "GLN - Giao hàng",
            "411" => "GLN - Vị trí vật lý",
            "412" => "GLN - Người xuất hóa đơn",
            "413" => "GLN - Số dư",
            "414" => "GLN - Hiện tại",
            "420" => "Mã quốc gia",
            "421" => "Mã quốc gia (có kiểm tra)",
            "422" => "Quốc gia xử lý",
            "423" => "Quốc gia xử lý ban đầu",
            "424" => "Quốc gia tháo gỡ",
            "7001" => "Ngày và thời gian sản xuất",
            "7002" => "Ngày và thời gian hết hạn",
            "7003" => "Ngày và thời gian đóng gói",
            "7004" => "Ngày và thời gian tốt nhất trước",
            "8001" => "GTIN của thành phần",
            "8002" => "Số serial của thành phần",
            "8003" => "GTIN của gói sản phẩm",
            "8004" => "Định danh tài sản toàn cầu (GIAI)",
            "8005" => "Giá mỗi đơn vị",
            "8006" => "ID các thành phần",
            "8017" => "Ngày và thời gian tốt nhất trước",
            "8018" => "Ngày và thời gian bán cuối cùng",
            "8110" => "Số lượng mặt hàng thương mại biến đổi",
            "90" => "Thông tin thỏa thuận chung",
            "91" => "Thông tin nội bộ công ty",
            "92" => "Thông tin nội bộ công ty",
            "93" => "Thông tin nội bộ công ty",
            "94" => "Thông tin nội bộ công ty",
            "95" => "Thông tin nội bộ công ty",
            "96" => "Thông tin nội bộ công ty",
            "97" => "Thông tin nội bộ công ty",
            "98" => "Thông tin nội bộ công ty",
            "99" => "Thông tin nội bộ công ty",
            _ => $"AI không xác định ({ai})"
        };
    }
}