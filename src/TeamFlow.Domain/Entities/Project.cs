using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.Entities
{
   public class Project
   {
      public const int NameMaxLength = 200;
      public const int DescriptionMaxLength = 4000;
      public Guid Id { get; private set; }
      public string Name { get; private set; }
      public string? Description { get; private set; }
      public DateTime StartDate { get; private set; }
      public DateTime? EndDate { get; private set; }
      public Enums.ProjectStatus Status { get; private set; } = Enums.ProjectStatus.Planned;
      public Guid CreatedByUserId { get; private set; }
      public DateTime CreatedAt { get; private set; }
      public DateTime? UpdatedAt { get; private set; }

      public Project(string name, string? description, DateTime startDate, DateTime? endDate, Guid createdByUserId, DateTime now)
      {
         Id = Guid.NewGuid();

         Name = ValidateName(name);
         Description = ValidateDescription(description);
         StartDate = startDate;
         EndDate = ValidateDates(startDate, endDate);
         CreatedByUserId = createdByUserId;
         CreatedAt = now;
      }

      private string ValidateName(string name)
      {
         var trimmedName = name?.Trim();

         if (string.IsNullOrWhiteSpace(trimmedName))
         {
            throw new BusinessRuleException("Project name cannot be empty.");
         }
         if (trimmedName.Length > NameMaxLength)
         {
            throw new BusinessRuleException($"Project name cannot exceed {NameMaxLength} characters.");
         }
         return trimmedName;
      }

      private string? ValidateDescription(string? description)
      {
         var trimmedDescription = description?.Trim();
         if (trimmedDescription != null && trimmedDescription.Length > DescriptionMaxLength)
         {
            throw new BusinessRuleException($"Project description cannot exceed {DescriptionMaxLength} characters.");
         }
         return string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription; 
      }

      private DateTime? ValidateDates(DateTime startDate, DateTime? endDate)
      {
         if (endDate.HasValue && endDate.Value < startDate)
         {
            throw new BusinessRuleException("End date cannot be earlier than start date.");
         }

         return endDate;
      }

      public void Archive(DateTime now)
      {
         if (Status == Enums.ProjectStatus.Archived)
            return;

         if (Status == Enums.ProjectStatus.Completed)
            Status = Enums.ProjectStatus.Archived;
         else
            throw new BusinessRuleException("Only completed projects can be archived.");

         UpdatedAt = now;
      }

      public void ChangeStatus(Enums.ProjectStatus newStatus, DateTime now)
      {
         EnsureNotArchived();
         if (newStatus == Enums.ProjectStatus.Archived)
         {
            throw new BusinessRuleException("Cannot change status to Archived state. Use Archive instead.");
         }   
        
         Status = newStatus;
         UpdatedAt = now;

      }

      public void Update(string name, string? description, DateTime startDate, DateTime? endDate, DateTime now)
      {
         EnsureNotArchived();

         Name = ValidateName(name);
         Description = ValidateDescription(description);
         StartDate = startDate;
         EndDate = ValidateDates(startDate, endDate);
         UpdatedAt = now;
      }

      private void EnsureNotArchived()
      {
         if (Status == Enums.ProjectStatus.Archived)
         {
            throw new BusinessRuleException("Cannot perform this operation on Archived projects.");
         }
      }
   }
}
