namespace WvsBeta.SharedDataProvider.Templates
{
    public class QuizData
    {
        public readonly byte Category;
        public readonly short Number;
        public readonly char Answer;

        public QuizData(byte category, short number, char answer)
        {
            Category = category;
            Number = number;
            Answer = answer;
        }
    }
}
