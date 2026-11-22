using System;
using System.Collections.Generic;
using System.Text;

namespace TeamFlow.Domain.Entities
{
   public class TaskComment
   {
      public const int ContentMaxLength = 2000;

      public Guid Id { get; private set; }
      public Guid TaskId { get; private set; }
      public Guid UserId { get; private set; }
      public string Content { get; private set; } = string.Empty;
      public DateTime CreatedAt { get; private set; }
      public DateTime? UpdatedAt { get; private set; }

      public TaskComment(Guid taskId, Guid userId, string content, DateTime now)
      {
         Id = Guid.NewGuid();
         TaskId = taskId;
         UserId = userId;
         Content = ValidateContent(content);
         CreatedAt = now;
         UpdatedAt = null;
      }

      private string ValidateContent(string content)
      {
         var trimmedContent = content?.Trim();
         if (string.IsNullOrWhiteSpace(trimmedContent))
         {
            throw new Exceptions.BusinessRuleException("Task comment content cannot be empty.");
         }
         if (trimmedContent.Length > ContentMaxLength)
         {
            throw new Exceptions.BusinessRuleException($"Task comment content cannot exceed {ContentMaxLength} characters.");
         }
         return trimmedContent;
      }

      public void UpdateContent(string newContent, DateTime now)
      {
         Content = ValidateContent(newContent);
         UpdatedAt = now;
      }      
   }
}
