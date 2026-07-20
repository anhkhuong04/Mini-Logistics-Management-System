namespace MiniLogistics.Domain.Shipments;

public enum FailureReasonCode
{
    ReceiverUnavailable = 1,
    WrongAddress = 2,
    ReceiverRejected = 3,
    CannotContactReceiver = 4,
    DamagedParcel = 5,
    PaymentIssue = 6,
    WeatherOrAccessIssue = 7,
    Other = 99
}
