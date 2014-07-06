﻿namespace PowerPointLabs
{
    internal class TextCollection
    {
        // Button Supertips
        public const string AddAnimationButtonSupertip =
            @"Adds a slide with animations to create an animated transition from the currently selected slide to the next slide.";
        public const string ReloadButtonSupertip =
            @"Recreates an existing animation slide with new animations.&#13;&#13;To activate, select the original slide or the animation slide (added previously) and click this button.";
        public const string InSlideAnimateButtonSupertip =
            @"Moves a shape around the slide in multiple steps.&#13;&#13;To activate, copy the shape to locations where you want it to stop, select the copies in the order you want them to appear, and click this button";
        public const string AddZoomInButtonSupertip =
            @"Adds a slide with animations to create a zoom-in effect from the currently selected shape to the next slide.&#13;&#13;To activate, select a rectangle shape on the slide that you want to drill down from and click on this button.";
        public const string AddZoomOutButtonSupertip =
            @"Adds a slide with animations to create a zoom-out effect from the previous slide to the currently selected shape.&#13;&#13;To activate, select a rectangle shape on the slide that you want to step back to and click on this button.";
        public const string ZoomToAreaButtonSupertip =
            @"Zoom into an area of a slide or image.&#13;&#13;To activate, place a rectangle shape on the portion that you want to magnify and click on this button.&#13;&#13;This feature works best with high-resolution images.";
        public const string MoveCropShapeButtonSupertip =
            @"Crop a picture according to your selected shape(s).&#13;&#13;To activate, draw a shape upon the picture you want to crop, select the shape, and then click on this button.";
        public const string AddSpotlightButtonSupertip =
            @"Creates a spotlight effect for a selected shape.&#13;&#13;To activate, draw a shape to match the spotlight you want, select the shape, and click this button.";
        public const string ReloadSpotlightButtonSupertip =
            @"Adjusts the transparency and edges of an existing spotlight.&#13;&#13;To activate, set the transparency level and soft edges width, select the existing spotlight shape, and click this button.";
        public const string AddAudioButtonSupertip =
            @"Generates synthesized audio from text in the Speaker Notes pane, and adds it to the slide(s).&#13;&#13;To add audios for multiple slides, select the slides first (e.g. Ctrl+A to select all slides, Ctrl+click to select only some slides) before clicking the 'Add Audio' button.";
        public const string GenerateRecordButtonSupertip =
            @"Generates synthesized audio from text in the Speaker Notes pane, and adds it to the slide(s).";
        public const string AddRecordButtonSupertip =
            @"Use this option to record audio manually to replace the generated audio.";
        public const string RemoveAudioButtonSupertip =
            @"Removes any generated audio that was added using Auto Narrate from the slide(s).&#13;&#13;To remove audios for multiple slides, select the slides first (e.g. Ctrl+A to select all slides, Ctrl+click to select only some slides) before clicking the 'Remove Audio' button.";
        public const string AddCaptionsButtonSupertip =
            @"Generates movie-style subtitles from text in the Speaker Notes pane, and adds it to the slide(s).&#13;&#13;To add captions for multiple slides, select the slides first (e.g. Ctrl+A to select all slides, Ctrl+click to select only some slides) before clicking the 'Add Captions' button.";
        public const string RemoveCaptionsButtonSupertip =
            @"Removes any captions that were added using Auto Captions from the slide(s).&#13;&#13;To remove captions for multiple slides, select the slides first (e.g. Ctrl+A to select all slides, Ctrl+click to select only some slides) before clicking the 'Remove Captions' button.";
        public const string HighlightBulletsTextButtonSupertip =
            @"Highlights all the bullet points on the specified slide by changing the text color.";
        public const string HighlightBulletsBackgroundButtonSupertip =
            @"Highlights all the bullet points on the specified slide by highlighting the text background";
        public const string ColorPickerButtonSupertip = @"Opens Custom Color Picker";
        public const string CustomeShapeButtonSupertip = @"Manage your custom shapes on the fly.";
        public const string HelpButtonSupertip = @"Click this to visit PowerPointLabs help page in our website.";
        public const string FeedbackButtonSupertip = @"Click this to email us problem reports or other feedback. ";
        public const string AboutButtonSupertip = @"Information about the PowerPointLabs plugin.";

        public const string AboutInfo =
            @"          PowerPointLabs Plugin Version 1.8 [Release date: 23 Jun 2014]\n     Developed at School of Computing, National University of Singapore.\n        For more information, visit our website http://PowerPointLabs.info";

        public const string AccessTempFolderErrorMsg = "Error when accessing temp folder";
        public const string CreatTempFolderErrorMsg = "Error when creating temp folder";
        public const string ExtraErrorMsg = "Error when extracting";
        public const string PrepareMediaErrorMsg = "Error when preparing media files";
        public const string VersionNotCompatibleMsg =
            "This file is not fully compatible with some features of PowerPointLabs because it is " +
            "in the outdated .ppt format used by PowerPoint 2007 (and older). If you wish to use the " +
            "full power of PowerPointLabs to enhance this file, please save in the .pptx format used " +
            "by PowerPoint 2010 and newer.";
    }
}
