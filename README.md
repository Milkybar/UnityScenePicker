# UnityScenePicker
Over the years, I've worked on a lot of Unity projects with some implementation of a scene-picking object reference tool and have always run into bugs with reliability or missing use cases. So here is my implementation.

![Preview of the tool in action](/Doc/preview.gif)

* Works correctly with arrays of object references
* Selection preview in the object reference field
* Scene highlighting for selection preview
* Inspector reference field highlight tint to notify selection mode target
* Escape/Right Mouse to cancel selection mode without applying changes
* Robust, wont break the editor on unexpected GUI input or object selection changes
* Works for all GameObject, component, and interface types
* Fast, wont slow the editor to a crawl!

Simply add the ```[ScenePicker]``` attribute to any serialized object reference to enable the scene picking button in the inspector.
```c#
[ScenePicker]
public AudioSource m_AudioSource;
```

Everything is released under the [MIT License](https://opensource.org/license/MIT).
