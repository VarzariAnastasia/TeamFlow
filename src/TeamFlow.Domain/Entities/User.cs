using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.Entities
{
   public class User
   {
      public const int NameMaxLength = 200;
      public const int EmailMaxLength = 256;

      public Guid Id { get; private set; }
      public string Name { get; private set; }
      public string Email { get; private set; }
      public Enums.UserRole Role { get; private set; }
      public bool IsActive { get; private set; }
      public DateTime CreatedAt { get; private set; }

      public User(string name, string email, Enums.UserRole role, DateTime now)
      {
         Id = Guid.NewGuid();
         Name = ValidateName(name);
         Email = ValidateEmail(email);
         Role = role;
         IsActive = true;
         CreatedAt = now;
      }

      private string ValidateName(string name)
      {
         var trimmedName = name?.Trim();

         if (string.IsNullOrWhiteSpace(trimmedName))
         {
            throw new BusinessRuleException("User name cannot be empty.");
         }
         if (trimmedName.Length > NameMaxLength)
         {
            throw new BusinessRuleException($"User name cannot exceed {NameMaxLength} characters.");
         }
         return trimmedName;
      }

      private string ValidateEmail(string email)
      {
         var trimmedEmail = email?.Trim();
         if (string.IsNullOrWhiteSpace(trimmedEmail))
         {
            throw new BusinessRuleException("User email cannot be empty.");
         }
         if (trimmedEmail.Length > EmailMaxLength)
         {
            throw new BusinessRuleException($"User email cannot exceed {EmailMaxLength} characters.");
         }

         return trimmedEmail.ToLowerInvariant();
      }

      public void ActivateUser()
      {
         if (IsActive)
         {
            return;
         }
         IsActive = true;
      }

      public void DeactivateUser()
      { 
         if(!IsActive)
         {
            return;
         }

         IsActive = false;
      }
     
      public void UpdateUser(string name, string email, Enums.UserRole role)
      {
         Name = ValidateName(name);
         Email = ValidateEmail(email);
         Role = role;
      }
   }
}
