using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class Question
    {
        [DataMember(Order = 1, IsRequired = true)]
        public Guid QuestionId { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public string Text { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public string Difficulty { get; set; }

    }
}