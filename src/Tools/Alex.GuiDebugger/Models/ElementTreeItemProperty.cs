﻿using System;

namespace Alex.GuiDebugger.Models
{
    public class ElementTreeItemProperty
    {
        public Guid ElementId { get; }

        public string Name { get; }
        
        public string Type { get; }
        
        public object Value { get; set; }

        public ElementTreeItemProperty()
        {

        }

        public ElementTreeItemProperty(Guid elementId, string name, string type, object value) : this()
        {
            ElementId = elementId;
            Name = name;
            Type = type;
            Value = value;
        }

    }
}
