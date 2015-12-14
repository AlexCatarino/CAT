﻿namespace CAT.Model
{
    using System;
    using System.ComponentModel;
    using System.Linq.Expressions;
    
    [Serializable]
    public class ObservableObject : INotifyPropertyChanged
    {
        /// <summary>
        /// Raises the property changed
        /// </summary>
        /// <param name="propertyExpression">property</param>
        protected void RaisePropertyChanged<T>(Expression<Func<T>> action)
        {
            var propertyName = GetPropertyName(action);
            RaisePropertyChanged(propertyName);
        }

        private static string GetPropertyName<T>(Expression<Func<T>> action)
        {
            var expression = (MemberExpression)action.Body;
            var propertyName = expression.Member.Name;
            return propertyName;
        }

        private void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
    }
}

