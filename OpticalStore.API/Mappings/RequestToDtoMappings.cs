using OpticalStore.API.Requests.Auth;
using OpticalStore.API.Requests.Feedbacks;
using OpticalStore.API.Requests.Lenses;
using OpticalStore.API.Requests.Notifications;
using OpticalStore.API.Requests.Orders;
using OpticalStore.API.Requests.Payments;
using OpticalStore.API.Requests.Permissions;
using OpticalStore.API.Requests.Products;
using OpticalStore.API.Requests.ProductVariants;
using OpticalStore.API.Requests.Roles;
using OpticalStore.API.Requests.Users;
using OpticalStore.BLL.DTOs.Auth;
using OpticalStore.BLL.DTOs.Feedbacks;
using OpticalStore.BLL.DTOs.Lenses;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.DTOs.Orders;
using OpticalStore.BLL.DTOs.Payments;
using OpticalStore.BLL.DTOs.Permissions;
using OpticalStore.BLL.DTOs.Products;
using OpticalStore.BLL.DTOs.ProductVariants;
using OpticalStore.BLL.DTOs.Roles;
using OpticalStore.BLL.DTOs.Users;

namespace OpticalStore.API.Mappings;

public static class RequestToDtoMappings
{
    public static LoginRequestDto ToDto(this LoginRequest request)
    {
        return new LoginRequestDto
        {
            Username = request.Username,
            Password = request.Password
        };
    }

    public static TokenRequestDto ToDto(this TokenRequest request)
    {
        return new TokenRequestDto
        {
            Token = request.Token
        };
    }

    public static UserRegistrationDto ToDto(this UserRegistrationRequest request)
    {
        return new UserRegistrationDto
        {
            Username = request.Username,
            Password = request.Password,
            Email = request.Email,
            Phone = request.Phone,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Dob = request.Dob,
            ImageUrl = request.ImageUrl
        };
    }

    public static UserUpdateDto ToDto(this UserUpdateRequest request)
    {
        return new UserUpdateDto
        {
            Password = request.Password,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Dob = request.Dob,
            Email = request.Email,
            Phone = request.Phone,
            ImageUrl = request.ImageUrl
        };
    }

    public static CreateRoleDto ToDto(this CreateRoleRequest request)
    {
        return new CreateRoleDto
        {
            Name = request.Name,
            Description = request.Description,
            Permissions = request.Permissions
        };
    }

    public static CreatePermissionDto ToDto(this CreatePermissionRequest request)
    {
        return new CreatePermissionDto
        {
            Name = request.Name,
            Description = request.Description
        };
    }

    public static ProductUpsertDto ToDto(this ProductRequest request)
    {
        return new ProductUpsertDto
        {
            Name = request.Name,
            Brand = request.Brand,
            Category = request.Category,
            FrameType = request.FrameType,
            Gender = request.Gender,
            Shape = request.Shape,
            FrameMaterial = request.FrameMaterial,
            HingeType = request.HingeType,
            NosePadType = request.NosePadType,
            WeightGram = request.WeightGram,
            Status = request.Status
        };
    }

    public static ProductVariantUpsertDto ToDto(this ProductVariantRequest request)
    {
        return new ProductVariantUpsertDto
        {
            ProductId = request.ProductId,
            ColorName = request.ColorName,
            FrameFinish = request.FrameFinish,
            LensWidthMm = request.LensWidthMm,
            BridgeWidthMm = request.BridgeWidthMm,
            TempleLengthMm = request.TempleLengthMm,
            SizeLabel = request.SizeLabel,
            Price = request.Price,
            Quantity = request.Quantity,
            Status = request.Status,
            OrderItemType = request.OrderItemType
        };
    }

    public static InventoryUpdateDto ToDto(this InventoryUpdateRequest request)
    {
        return new InventoryUpdateDto
        {
            ProductVariantId = request.ProductVariantId,
            ChangeAmount = request.ChangeAmount
        };
    }

    public static CreateLensDto ToDto(this CreateLensRequest request)
    {
        return new CreateLensDto
        {
            Name = request.Name,
            Material = request.Material,
            Price = request.Price,
            Description = request.Description
        };
    }

    public static CreateNotificationDto ToDto(this CreateNotificationRequest request)
    {
        return new CreateNotificationDto
        {
            RecipientId = request.RecipientId,
            Title = request.Title,
            Content = request.Content
        };
    }

    public static PaymentRequirementDto ToDto(this PaymentRequirementRequest request)
    {
        return new PaymentRequirementDto
        {
            Items = request.Items.Select(x => x.ToDto()).ToList()
        };
    }

    public static PaymentRequirementItemDto ToDto(this PaymentRequirementItemRequest request)
    {
        return new PaymentRequirementItemDto
        {
            ProductVariantId = request.ProductVariantId,
            LensId = request.LensId,
            Quantity = request.Quantity
        };
    }

    public static FeedbackCreateDto ToDto(this FeedbackCreateRequest request)
    {
        return new FeedbackCreateDto
        {
            OrderId = request.OrderId,
            ProductId = request.ProductId,
            Rating = request.Rating,
            Comment = request.Comment
        };
    }

    public static FeedbackUpdateDto ToDto(this FeedbackUpdateRequest request)
    {
        return new FeedbackUpdateDto
        {
            Rating = request.Rating,
            Comment = request.Comment
        };
    }

    public static CreateOrderDto ToDto(this CreateOrderRequest request)
    {
        return new CreateOrderDto
        {
            DeliveryAddress = request.DeliveryAddress,
            RecipientName = request.RecipientName,
            PhoneNumber = request.PhoneNumber,
            Items = request.Items.Select(x => x.ToDto()).ToList(),
            BankInfo = request.BankInfo?.ToDto()
        };
    }

    public static CreateOrderItemDto ToDto(this CreateOrderItemRequest request)
    {
        return new CreateOrderItemDto
        {
            ProductVariantId = request.ProductVariantId,
            Quantity = request.Quantity,
            LensId = request.LensId,
            Prescription = request.Prescription?.ToDto()
        };
    }

    public static UpdateOrderDto ToDto(this UpdateOrderRequest request)
    {
        return new UpdateOrderDto
        {
            DeliveryAddress = request.DeliveryAddress,
            RecipientName = request.RecipientName,
            PhoneNumber = request.PhoneNumber,
            Items = request.Items.Select(x => x.ToDto()).ToList()
        };
    }

    public static UpdateOrderItemDto ToDto(this UpdateOrderItemRequest request)
    {
        return new UpdateOrderItemDto
        {
            OrderItemId = request.OrderItemId,
            Quantity = request.Quantity,
            Prescription = request.Prescription?.ToDto()
        };
    }

    public static PrescriptionDto ToDto(this PrescriptionRequest request)
    {
        return new PrescriptionDto
        {
            ImageUrl = request.ImageUrl,
            OdSphere = request.OdSphere,
            OdCylinder = request.OdCylinder,
            OdAxis = request.OdAxis,
            OdAdd = request.OdAdd,
            OdPd = request.OdPd,
            OsSphere = request.OsSphere,
            OsCylinder = request.OsCylinder,
            OsAxis = request.OsAxis,
            OsAdd = request.OsAdd,
            OsPd = request.OsPd,
            Note = request.Note
        };
    }

    public static PriceCheckDto ToDto(this PriceCheckRequest request)
    {
        return new PriceCheckDto
        {
            Items = request.Items.Select(x => x.ToDto()).ToList()
        };
    }

    public static PriceCheckItemDto ToDto(this PriceCheckItemRequest request)
    {
        return new PriceCheckItemDto
        {
            ProductVariantId = request.ProductVariantId,
            Quantity = request.Quantity
        };
    }

    public static BankInfoDto ToDto(this BankInfoRequest request)
    {
        return new BankInfoDto
        {
            BankName = request.BankName,
            BankAccountNumber = request.BankAccountNumber,
            AccountHolderName = request.AccountHolderName
        };
    }
}
