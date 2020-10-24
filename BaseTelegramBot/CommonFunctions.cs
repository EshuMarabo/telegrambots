using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BaseTelegramBot
{

    public static class CommonFunctions
    {
        public static string[] CteateReturningValuesFromKeyboard(InlineKeyboardMarkup keyboard)
        {
            if (keyboard == null)
            {
                return null;
            }
            else
            {
                int element_counter = 0;
                foreach (var button_row in keyboard.InlineKeyboard)
                    foreach (var button in button_row)
                        element_counter++;
                    string[] result = new string[element_counter];
                int i = 0;
                foreach (var button_row in keyboard.InlineKeyboard)
                {
                    foreach (var button in button_row)
                    {
                        result[i] = button.CallbackData;
                        i++;
                    }
                }
                return result;
            }
        }
        public static InlineKeyboardMarkup CreateInlineKeyboard(List<List<string>> ReturningValues)
        {
            List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();
            foreach (List<string> row in ReturningValues)
            {
                List<InlineKeyboardButton> ButtonsInRow = new List<InlineKeyboardButton>();
                foreach (string ReturningData in row)
                {
                    ButtonsInRow.Add(new InlineKeyboardButton() { CallbackData = ReturningData, Text = ReturningData });

                }
                buttons.Add(ButtonsInRow);

            }
            return new InlineKeyboardMarkup(buttons);
        }
        public static InlineKeyboardMarkup CreateInlineKeyboard(List<List<string>> Texts, List<List<string>> ReturningValues)
        {
            List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();
          
            for (int i=0;i<ReturningValues.Count;i++)
            {
                List<InlineKeyboardButton> ButtonsInRow = new List<InlineKeyboardButton>();
                for (int j=0;j<ReturningValues[i].Count;j++)
                {
                    ButtonsInRow.Add(new InlineKeyboardButton() { CallbackData = ReturningValues[i][j], Text = Texts[i][j] });

                }
                buttons.Add(ButtonsInRow);

            }
            return new InlineKeyboardMarkup(buttons);
        }
        public class EntityComparer : IComparer<MessageEntity>
        {
            int IComparer<MessageEntity>.Compare(MessageEntity x, MessageEntity y)
            {
                if (x.Offset > y.Offset)
                {
                    return 1;
                }
                else if (x.Offset < y.Offset)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
        }
        public class UserComparer : IEqualityComparer<User>
        {
            bool IEqualityComparer<User>.Equals(User x, User y)
            {
                return x.Id == y.Id;
            }

            int IEqualityComparer<User>.GetHashCode(User obj)
            {
                return obj.GetHashCode();

            }
        }


        public static string ApplyFormat(MessageEntity entity, string StartSymbols, string EndSymbols, string startText, ref int offset)
        {
            string newText = string.Empty;
            for (int iter = 0; iter < startText.Length; iter++)
            {
                if (iter == entity.Offset + offset)
                {
                    newText += StartSymbols;
                }
                if (iter == entity.Offset + entity.Length + offset)
                {
                    newText += EndSymbols;
                }
                newText += startText[iter];
            }
            if (entity.Offset + entity.Length + offset == startText.Length)
                newText += EndSymbols;
            offset += StartSymbols.Length;
            offset += EndSymbols.Length;
            return newText;
        }

        public static string TextFormatingRecovering(MessageEntity[] Entities, string text)
        {
            if (Entities == null|| text==null) return text;
            int offset = 0;
            Array.Sort(Entities, new EntityComparer());//TODO удостовериться, что они всегда сортированные
            foreach (MessageEntity entity in Entities)
            {
                string StartSymbols = string.Empty;
                string EndSymbols = string.Empty;
                switch (entity.Type)
                {
                    case MessageEntityType.Bold:
                        StartSymbols = "<b>";// "*";
                        EndSymbols = "</b>";// "*";
                        break;
                    case MessageEntityType.Italic:
                        StartSymbols = "<i>";// "_";
                        EndSymbols = "</i>";//"_";
                        break;
                    case MessageEntityType.Strikethrough:
                        StartSymbols = "<strike>";//"-";
                        EndSymbols = "</strike>";
                        break;
                    case MessageEntityType.Underline:
                        StartSymbols = "<u>";
                        EndSymbols = "</u>";
                        break;
                    case MessageEntityType.Code:
                        StartSymbols = "<code>";//"'"
                        EndSymbols = "</code>";
                        break;
                    case MessageEntityType.Pre:
                        StartSymbols = "<pre>";
                        EndSymbols = "</pre>";
                        break;
                    case MessageEntityType.TextLink:
                        StartSymbols = "<a href=\"" + entity.Url + "\">";
                        EndSymbols = "</a>";
                        break;
                    case MessageEntityType.TextMention:
                        StartSymbols = "<a href=\"tg://user?id=" + entity.User.Id.ToString() + "\">";
                        EndSymbols = "</a>";
                        break;
                }
                text = ApplyFormat(entity, StartSymbols, EndSymbols, text, ref offset);
            }
            return text;
        }
    }
}
