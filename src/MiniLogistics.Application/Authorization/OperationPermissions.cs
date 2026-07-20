using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Authorization;

public static class OperationPermissions
{
    public const string ShipmentView = "operations.shipment.view";
    public const string AssignmentAssign = "operations.assignment.assign";
    public const string AssignmentRetryAuto = "operations.assignment.retry_auto";
    public const string AssignmentBulkRetryAuto = "operations.assignment.bulk_retry_auto";
    public const string AssignmentReassign = "operations.assignment.reassign";
    public const string AssignmentCancel = "operations.assignment.cancel";
    public const string ShipmentStatusUpdate = "operations.shipment.status_update";
    public const string ShipmentProofSubmit = "operations.shipment.proof_submit";
    public const string ShipmentProofView = "operations.shipment.proof_view";
    public const string CodCollect = "operations.cod.collect";
    public const string CodViewPending = "operations.cod.view_pending";
    public const string CodSettle = "operations.cod.settle";
    public const string AuditView = "operations.audit.view";

    private static readonly IReadOnlySet<string> AdminPermissions = new HashSet<string>
    {
        ShipmentView,
        AssignmentAssign,
        AssignmentRetryAuto,
        AssignmentBulkRetryAuto,
        AssignmentReassign,
        AssignmentCancel,
        ShipmentStatusUpdate,
        ShipmentProofSubmit,
        ShipmentProofView,
        CodCollect,
        CodViewPending,
        CodSettle,
        AuditView
    };

    private static readonly IReadOnlySet<string> OperatorPermissions = new HashSet<string>
    {
        ShipmentView,
        AssignmentAssign,
        AssignmentRetryAuto,
        AssignmentBulkRetryAuto,
        AssignmentReassign,
        AssignmentCancel,
        ShipmentStatusUpdate,
        ShipmentProofSubmit,
        ShipmentProofView,
        CodCollect,
        CodViewPending
    };

    public static IReadOnlyCollection<string> ForRole(string role)
    {
        if (string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissions.ToArray();
        }

        if (string.Equals(role, nameof(UserRole.Operator), StringComparison.OrdinalIgnoreCase))
        {
            return OperatorPermissions.ToArray();
        }

        return [];
    }
}
