using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.Entities
{
   public class TaskItem
   {

      private const int DescriptionMaxLength = 4000;
      private const int TaskTitleMaxLength = 200;

      public Guid Id { get; private set; }
      public Guid ProjectId { get; private set; }
      public string Title { get; private set; }
      public string? Description { get; private set; }
      public Guid? AssignedToUserId { get; private set; } = null;
      public Guid CreatedByUserId { get; private set; }
      public Enums.TaskItemStatus Status { get; private set; } = Enums.TaskItemStatus.ReadyForDevelopment;
      public Enums.TaskPriority Priority { get; private set; } = Enums.TaskPriority.Medium;
      public DateTime? DueDate { get; private set; }
      public DateTime CreatedAt { get; private set; }
      public DateTime UpdatedAt { get; private set; }
      public DateTime? CompletedAt { get; private set; }
      public bool IsArchived { get; private set; } = false;

      public TaskItem(Guid projectId, string title, string description, Guid createdByUserId, DateTime? dueDate, DateTime now)
      {
         Id = Guid.NewGuid();
         ProjectId = projectId;
         Title = ValidateTitle(title);
         Description = ValidateTaskDescription(description);
         CreatedByUserId = createdByUserId;
         ValidateDueDate(dueDate, now);
         DueDate = dueDate;
         CreatedAt = now;
         UpdatedAt = now;
      }

      private void ValidateDueDate(DateTime? dueDate, DateTime now)
      {
         if (dueDate < now)
         {
            throw new Exceptions.BusinessRuleException("Due date cannot be in the past.");
         }
      }

      public void Update(string title, string description, Guid? assignedToUserId, Enums.TaskItemStatus status, Enums.TaskPriority priority, DateTime? dueDate, DateTime now)
      {
         Title = ValidateTitle(title);
         Description = ValidateTaskDescription(description);
         AssignedToUserId = assignedToUserId;
         ValidateStatusTransition(status, now);
         Status = status;
         Priority = priority;
         DueDate = dueDate;
         UpdatedAt = now;
      }

      private void ValidateStatusTransition(Enums.TaskItemStatus newStatus, DateTime now)
      {
         if (Status == newStatus)
            return;

         if (Status == Enums.TaskItemStatus.Done && newStatus != Enums.TaskItemStatus.Done)
         {
            CompletedAt = null;
         }
         if (newStatus == Enums.TaskItemStatus.Done && Status != Enums.TaskItemStatus.Done)
         {
            CompletedAt = now;
         }
      }

      private string ValidateTitle(string title)
      {
         var trimmedTitle = title?.Trim();
         if (string.IsNullOrWhiteSpace(trimmedTitle))
         {
            throw new Exceptions.BusinessRuleException("Task title cannot be empty.");
         }
         if (trimmedTitle.Length > TaskTitleMaxLength)
         {
            throw new Exceptions.BusinessRuleException($"Task title cannot exceed {TaskTitleMaxLength} characters.");
         }
         return trimmedTitle;
      }

      private string? ValidateTaskDescription(string? description)
      {
         var trimmedDescription = description?.Trim();
         if (trimmedDescription != null && trimmedDescription.Length > DescriptionMaxLength)
         {
            throw new Exceptions.BusinessRuleException($"Task description cannot exceed {DescriptionMaxLength} characters.");
         }
         return string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;
      }

      public void AssignToUser(Guid userId, DateTime now)
      {
         if (IsArchived)
            return;
         AssignedToUserId = userId;
         UpdatedAt = now;
      }

      public void UnassignUser(DateTime now)
      {
         AssignedToUserId = null;
         UpdatedAt = now;
      }

      public void ArchiveTask(DateTime now)
      {
         if (IsArchived)
            return;
         IsArchived = true;
         UpdatedAt = now;
      }

      public bool IsOverdue(DateTime? now)
      {
         return DueDate <= now && Status != Enums.TaskItemStatus.Done;
      }
   }
}
