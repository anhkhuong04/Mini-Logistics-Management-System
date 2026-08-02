namespace MiniLogistics.Application.Common;

public interface IPiiMaskingService
{
    string? MaskSensitiveJson(string? json);

    string? MaskSensitiveText(string? text);

    string MaskPhone(string phoneNumber);
}
