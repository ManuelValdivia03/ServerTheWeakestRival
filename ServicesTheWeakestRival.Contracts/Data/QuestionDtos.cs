using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class QuestionDto
    {
        [DataMember(Order = 1, IsRequired = true)]
        public Guid QuestionId { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public string Text { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public string Difficulty { get; set; }

    }

    [DataContract]
    public sealed class AnswerDto
    {
        [DataMember(Order = 1)] public int AnswerId { get; set; }
        [DataMember(Order = 2)] public string Text { get; set; }
        [DataMember(Order = 3)] public bool IsCorrect { get; set; }
        [DataMember(Order = 4)] public byte DisplayOrder { get; set; }
    }

    [DataContract]
    public sealed class QuestionWithAnswersDto
    {
        [DataMember(Order = 1)] public int QuestionId { get; set; }
        [DataMember(Order = 2)] public int CategoryId { get; set; }
        [DataMember(Order = 3)] public byte Difficulty { get; set; }
        [DataMember(Order = 4)] public string LocaleCode { get; set; }
        [DataMember(Order = 5)] public string Body { get; set; }
        [DataMember(Order = 6)] public List<AnswerDto> Answers { get; set; } = new List<AnswerDto>();
    }

    [DataContract]
    public sealed class GetQuestionsRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public byte Difficulty { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public string LocaleCode { get; set; }

        [DataMember(Order = 4)]
        public int? MaxQuestions { get; set; }

        [DataMember(Order = 5)]
        public int? CategoryId { get; set; }
    }

    [DataContract]
    public sealed class GetQuestionsResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public List<QuestionWithAnswersDto> Questions { get; set; } = new List<QuestionWithAnswersDto>();
    }
}