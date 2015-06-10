﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.PowerPoint;
using PowerPointLabs.Models;
using PowerPointLabs.Utils;
using PowerPointLabs.Views;
using Shape = Microsoft.Office.Interop.PowerPoint.Shape;

namespace PowerPointLabs
{
    internal static class AgendaLab
    {
        private const string PptLabsAgendaTitleShapeName = "PptLabsAgendaTitle";
        private const string PptLabsAgendaContentShapeName = "PptLabsAgendaContent";
        private const string PptLabsAgendaBulletLinkShape = "PptLabsAgendaBulletLinkShape";

        private const string PptLabsAgendaVisualSectionName = "PptLabsAgendaVisualSection";
        private const string PptLabsAgendaVisualItemPrefix = "PptLabsAgendaVisualItem";

        private const string PptLabsAgendaBeamBackgroundName = "PptLabsAgendaBeamBackground";
        private const string PptLabsAgendaBeamShapeName = "PptLabsAgendaBeamShape";
        private const string PptLabsAgendaBeamHighlight = "PptLabsAgendaBeamHighlight";

        private const float VisualAgendaItemMargin = 0.05f;

        private static LoadingDialog _loadDialog = new LoadingDialog();

        private static readonly string SlideCapturePath = Path.Combine(Path.GetTempPath(), "PowerPointLabs Temp");

        private static string _agendaText;

        private static bool _agendaOutdated;

        private static TextRange2 _bulletDefaultFormat;
        private static TextRange2 _bulletHighlightFormat;
        private static TextRange2 _bulletDimFormat;

        # region AgendaSlide class for Encoding/Decoding names
        /// <summary>
        /// An AgendaSlide object carries slide metadata.
        /// </summary>
        public class AgendaSlide
        {
            private static readonly string[] Delim = {"_&_"};
            private const string Prefix = "PptLabsAgenda";

            public readonly Type AgendaType;
            public readonly Purpose SlidePurpose;
            public readonly string Section;

            private AgendaSlide(Type type, Purpose purpose, string section)
            {
                AgendaType = type;
                SlidePurpose = purpose;
                Section = section;
            }

            /// <summary>
            /// Universal Encode function used for all Agenda Slides.
            /// Packs a set of agenda slide properties to a slide name. Paired with the Decode function.
            /// </summary>
            public static string Encode(Type agendaType, Purpose slidePurpose, string section)
            {
                string[] parameters = {Prefix, agendaType.ToString(), slidePurpose.ToString(), section};
                return string.Join(Delim[0], parameters);
            }

            /// <summary>
            /// Universal Encode function used for all Agenda Slides.
            /// Unpacks a slide name into a set of slide properties. Paired wit hthe Encode function.
            /// </summary>
            public static AgendaSlide Decode(string slide)
            {
                string[] parameters = slide.Split(Delim, StringSplitOptions.None);

                if (parameters[0] != Prefix) return null;
                if (parameters.Length != 4) return null;

                Type type;
                Purpose purpose;
                if (!Enum.TryParse(parameters[1], out type)) return null;
                if (!Enum.TryParse(parameters[2], out purpose)) return null;
                string section = parameters[3];

                return new AgendaSlide(type, purpose, section);
            }

            public static AgendaSlide Decode(PowerPointSlide slide)
            {
                if (slide == null) return null;
                return Decode(slide.Name);
            }

            public static AgendaSlide Decode(Slide slide)
            {
                if (slide == null) return null;
                return Decode(slide.Name);
            }

            /// <summary>
            /// Stores metadata in the slide by setting its name.
            /// </summary>
            public static void SetSlideName(PowerPointSlide slide, Type agendaType, Purpose slidePurpose, string section)
            {
                slide.Name = Encode(agendaType, slidePurpose, section);
            }

            /// <summary>
            /// Sets the name to that used by the reference (template) slide.
            /// </summary>
            public static void SetAsReferenceSlideName(PowerPointSlide slide, Type type)
            {
                slide.Name = Encode(type, Purpose.Reference, string.Empty);
            }

            public static bool IsReferenceslide(PowerPointSlide slide)
            {
                var agendaSlide = Decode(slide);
                if (agendaSlide == null) return false;

                return agendaSlide.SlidePurpose == Purpose.Reference;
            }

            public static bool IsReferenceslide(Slide slide)
            {
                var agendaSlide = Decode(slide);
                if (agendaSlide == null) return false;

                return agendaSlide.SlidePurpose == Purpose.Reference;
            }

            // convenience method.
            public static bool IsNotReferenceslide(PowerPointSlide slide)
            {
                return !IsReferenceslide(slide);
            }

            // convenience method.
            public static bool IsNotReferenceslide(Slide slide)
            {
                return !IsReferenceslide(slide);
            }

            /// <summary>
            /// Duplicate function of MeetsConditions2 to return a function (PowerPointSlide => bool) instead of (Slide => bool).
            /// 
            /// Example Usage:
            /// <code>
            ///     var condition = AgendaSlide.MeetsConditions(
            ///            agendaSlide =>
            ///                agendaSlide.AgendaType == Type.Visual &&
            ///                agendaSlide.SlidePurpose == Purpose.VisualAgendaSection);
            ///     return powerPointSlides.Where(condition);
            /// </code>
            /// </summary>
            /// <param name="condition">Input a condition on (AgendaSlide : bool)</param>
            /// <returns>Output a condition on (PowerPointSlide : bool).</returns>
            public static Func<PowerPointSlide, bool> MeetsConditions(Func<AgendaSlide, bool> condition)
            {
                return slide =>
                {
                    var agendaSlide = Decode(slide);
                    if (agendaSlide == null) return false;
                    return condition(agendaSlide);
                };
            }

            /// <summary>
            /// Same as MeetConditions, but returns a function from Slide to bool instead.
            /// </summary>
            /// <param name="condition">Input a condition on (AgendaSlide : bool)</param>
            /// <returns>Output a condition on (Slide : bool).</returns>
            public static Func<Slide, bool> MeetsConditions2(Func<AgendaSlide, bool> condition)
            {
                return slide =>
                {
                    var agendaSlide = Decode(slide);
                    if (agendaSlide == null) return false;
                    return condition(agendaSlide);
                };
            }

            public static bool IsAnyAgendaSlide(PowerPointSlide slide)
            {
                return Decode(slide) != null;
            }

            public static bool IsAnyAgendaSlide(Slide slide)
            {
                return Decode(slide) != null;
            }
        }
        # endregion


        # region Enum
        public enum Type
        {
            None,
            Bullet,
            Beam,
            Visual,
            Mixed
        };

        public enum Purpose
        {
            None,
            Reference,
            // For Bullet Agenda
            Start,
            End,
            // For Visual Agenda
            ZoomIn,
            ZoomOut,
            VisualAgendaSection,
            EndOfVisualAgenda
        };

        public enum Direction
        {
            Top,
            Left,
            Bottom,
            Right
        };
        # endregion

        /*********************************************************
         * Note:
         * The implementation of CurrentType is O(n),
         * bear this in mind when design functions. E.g. 
         * FindSectionEndSlide is totally fine with only the first
         * argument and check CurrentType in the function.
         * But taking type as the second argument allows the 
         * developer to get CurrentType before the function.
         * This is useful when CurrentType is frequently used
         * in a certain region, which reduces the overhead.
         * 
         * See SynchronizeAgenda() for more usage details.
         *********************************************************/
        # region Properties
        public static Type CurrentType
        {
            get
            {
                if (PowerPointPresentation.Current.SectionProperties.Count == 0)
                {
                    return Type.None;
                }

                var slides = PowerPointPresentation.Current.Slides;

                foreach (var slide in slides)
                {
                    var agendaSlide = AgendaSlide.Decode(slide);
                    if (agendaSlide != null)
                    {
                        return agendaSlide.AgendaType;
                    }

                    // here we try to find the first occurance, or potential occurance of beam type agenda.
                    // A potential occurance of beam type agenda is a slide that contains a group shape which
                    // contains a shape with PptLabsAgendaBeamBackgroundName as name. We need to do this since
                    // a user may want to ungroup a beam agenda and change the item's format, after he re-group
                    // the shape, the original name will be changed, and thus need to be renamed for easier
                    // recognition.
                    var beamShape = FindBeamShape(slide);

                    if (beamShape != null)
                    {
                        return Type.Beam;
                    }
                }

                return Type.None;
            }
        }

        public static Direction BeamDirection { get; set; }
        # endregion

        # region API
        public static void GenerateAgenda(Type type)
        {
            // TODO: Respect user's selection when generating a beam agenda while automatically removing previous agenda. Details in github commit comment (the commit after #571ce9a).

            bool afterRemovingPreviousAgenda = false;
            // agenda exists in current presentation
            if (CurrentType != Type.None)
            {
                var confirm = MessageBox.Show(TextCollection.AgendaLabAgendaExistError,
                                              TextCollection.AgendaLabAgendaExistErrorCaption,
                                              MessageBoxButtons.OKCancel);

                if (confirm != DialogResult.OK) return;

                RemoveAgenda();
                afterRemovingPreviousAgenda = true;
            }

            // validate section information
            if (!SectionValidation()) return;

            var selectedSlides = PowerPointCurrentPresentationInfo.SelectedSlides.ToList();
            bool userIsSelectingSlides = PowerPointCurrentPresentationInfo.CurrentSelection.Type == PpSelectionType.ppSelectionSlides;


            selectedSlides.RemoveAll(PowerPointAckSlide.IsAckSlide);
            PowerPointPresentation.Current.RemoveAckSlide();

            var sections = PowerPointPresentation.Current.Sections.Skip(1).ToList();

            _loadDialog = new LoadingDialog(TextCollection.AgendaLabLoadingDialogTitle,
                                                TextCollection.AgendaLabLoadingDialogContent);
            _loadDialog.Show();
            _loadDialog.Refresh();

            var curWindow = Globals.ThisAddIn.Application.ActiveWindow;
            var oldViewType = curWindow.ViewType;

            curWindow.ViewType = PpViewType.ppViewNormal;

            switch (type)
            {
                case Type.Beam:
                    if (!afterRemovingPreviousAgenda && userIsSelectingSlides && selectedSlides.Any())
                        GenerateBeamAgenda(sections, selectedSlides);
                    else
                        GenerateBeamAgenda(sections);
                    break;
                case Type.Bullet:
                    GenerateBulletAgenda(sections);
                    break;
                case Type.Visual:
                    GenerateVisualAgenda(sections);
                    break;
            }

            PowerPointPresentation.Current.AddAckSlide();

            curWindow.ViewType = oldViewType;
            var firstSlide = PowerPointPresentation.Current.FirstSlide;
            SelectOriginalSlide(selectedSlides.Count > 0 ? selectedSlides[0] : firstSlide, firstSlide);

            _loadDialog.Dispose();

            var moveSlides = PowerPointPresentation.Current.Slides.GetRange(3, 5);
            moveSlides.Reverse();
            foreach (var slide in moveSlides)
            {
                slide.MoveTo(8);
            }
        }

        public static void RemoveAgenda()
        {
            var type = CurrentType;

            if (type == Type.None)
            {
                MessageBox.Show(TextCollection.AgendaLabNoAgendaError);
                return;
            }

            var selectedSlides = PowerPointCurrentPresentationInfo.SelectedSlides.ToList();
            var slides = PowerPointPresentation.Current.Slides;

            switch (type)
            {
                case Type.Beam:
                    RemoveBeamAgenda();
                    break;
                case Type.Bullet:
                    RemoveBulletAgenda();
                    break;
                case Type.Visual:
                    RemoveVisualAgenda();
                    break;
            }

            var firstSlide = PowerPointPresentation.Current.FirstSlide;
            SelectOriginalSlide(selectedSlides.Count > 0 ? selectedSlides[0] : firstSlide, firstSlide);
        }

        public static void SynchronizeAgenda()
        {
            var type = CurrentType;
            var refSlide = FindReferenceSlide(type);

            if (type == Type.None)
            {
                MessageBox.Show(TextCollection.AgendaLabNoAgendaError);
                return;
            }
            if (AgendaSlide.IsNotReferenceslide(refSlide))
            {
                // no reference slide
                MessageBox.Show(TextCollection.AgendaLabNoReferenceSlideError);
                return;
            }

            if (!SectionValidation()) return;

            var selectedSlides = PowerPointCurrentPresentationInfo.SelectedSlides.ToList();
            bool userIsSelectingSlides = PowerPointCurrentPresentationInfo.CurrentSelection.Type == PpSelectionType.ppSelectionSlides;


            var curWindow = Globals.ThisAddIn.Application.ActiveWindow;
            var oldViewType = curWindow.ViewType;

            curWindow.ViewType = PpViewType.ppViewNormal;

            // find the agenda for the first section as reference
            var currentPresentation = PowerPointPresentation.Current;
            var sections = currentPresentation.Sections.Where(section =>
                                                              section != PptLabsAgendaVisualSectionName).Skip(1).ToList();

            if (AgendaSlide.IsNotReferenceslide(refSlide))
            {
                MessageBox.Show(TextCollection.AgendaLabNoReferenceError);
            }

            _loadDialog = new LoadingDialog("Synchronizing...", "Agenda is getting synchronized, please wait...");
            _loadDialog.Show();
            _loadDialog.Refresh();

            selectedSlides.RemoveAll(PowerPointAckSlide.IsAckSlide);
            currentPresentation.RemoveAckSlide();

            //TODO: UNCOMMENT
            PrepareSync(type, ref refSlide);
            Debug.WriteLine(_agendaOutdated);
            _agendaOutdated = false;

            try
            {
                // regenerate slides and sync accordingly
                switch (type)
                {
                    case Type.Beam:
                        if (userIsSelectingSlides)
                            SyncAgendaBeam(sections, refSlide, selectedSlides);
                        else
                            SyncAgendaBeam(sections, refSlide);
                        break;
                    case Type.Bullet:
                        SyncAgendaBullet(sections, refSlide);
                        break;
                    case Type.Visual:
                        CheckAgendaUpdate(Type.Visual, refSlide);
                        SyncAgendaVisual(sections, refSlide);
                        break;
                }

                PowerPointPresentation.Current.AddAckSlide();
                curWindow.ViewType = oldViewType;
                SelectOriginalSlide(selectedSlides.Count == 0 ? null : selectedSlides[0],
                                    PowerPointPresentation.Current.Slides[0]);
            }
            catch (Exception e)
            {
                ErrorDialogWrapper.ShowDialog("Unexpected error", e.Message, e);
            }
            finally
            {
                _loadDialog.Dispose();
            }
        }
        # endregion

        # region Helper Functions
        private static void AddAgendaSlideBeamType(string section, PowerPointSlide slide)
        {
            var beams = slide.Shapes.Paste()[1].Ungroup();

            var currentTextBox = beams.Cast<Shape>().FirstOrDefault(box => box.TextFrame
                                                                              .TextRange
                                                                              .Text == section);

            if (currentTextBox != null)
            {
                currentTextBox.TextFrame.TextRange.Font.Color.RGB = Utils.Graphics.ConvertColorToRgb(Color.Yellow);
                currentTextBox.Name += " " + PptLabsAgendaBeamHighlight;
            }

            beams.Group().Name = PptLabsAgendaBeamShapeName;
        }

        private static PowerPointSlide AddAgendaSlideBulletType(string section, bool isEnd, PowerPointSlide refSlide)
        {
            var sectionIndex = FindSectionAbsoluteIndex(section);
            var sectionEndIndex = FindSectionEnd(section);

            var slide =
                PowerPointSlide.FromSlideFactory(PowerPointPresentation.Current
                                                                       .Presentation
                                                                       .Slides
                                                                       .Add(isEnd && refSlide != null ? sectionEndIndex + 1 : 1,
                                                                            PpSlideLayout.ppLayoutText)
                                                                            , includeIndicator: true);

            slide.Transition.EntryEffect = PpEntryEffect.ppEffectFadeSmoothly;
            slide.Transition.Duration = 0.25f;

            slide.Shapes.Placeholders[1].Name = PptLabsAgendaTitleShapeName;
            slide.Shapes.Placeholders[2].Name = PptLabsAgendaContentShapeName;

            // set title
            slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Agenda";
            
            var contentPlaceHolder = slide.Shapes.Placeholders[2];
            var textRange = contentPlaceHolder.TextFrame2.TextRange;

            textRange.Text = _agendaText;

            if (refSlide != null)
            {
                AgendaSlide.SetSlideName(slide, Type.Bullet, isEnd ? Purpose.End : Purpose.Start, section);
                slide.Design = refSlide.Design;

                // since section index is 1-based, focus section index should be substracted by 1
                ReformatTextRange(textRange, sectionIndex - 1);

                if (!isEnd)
                {
                    slide.GetNativeSlide().MoveToSectionStart(sectionIndex);
                }
            }
            else
            {
                AgendaSlide.SetAsReferenceSlideName(slide, Type.Bullet);
                slide.AddTemplateSlideMarker();
                slide.Hidden = true;
                slide.DeleteIndicator();
                
                PickupBulletFormats();

                _bulletDimFormat.Font.Fill.ForeColor.RGB = Utils.Graphics.ConvertColorToRgb(Color.Gray);
                _bulletHighlightFormat.Font.Fill.ForeColor.RGB = Utils.Graphics.ConvertColorToRgb(Color.Red);
                _bulletDefaultFormat.Font.Fill.ForeColor.RGB = Utils.Graphics.ConvertColorToRgb(Color.Black);
            }

            return slide;
        }

        private static PowerPointSlide AddAgendaSlideVisualType(List<string> sections, PowerPointSlide refSlide,
                                                                int slideIndex, int relativeSectionIndex)
        {
            var currentPresentation = PowerPointPresentation.Current.Presentation;
            var slide = PowerPointSlide.FromSlideFactory(currentPresentation.Slides.Add(1, PpSlideLayout.ppLayoutTitleOnly), includeIndicator: true);

            PrepareVisualAgendaSlideShapes(slide, sections);

            if (refSlide == null)
            {
                AgendaSlide.SetAsReferenceSlideName(slide, Type.Visual);
                slide.AddTemplateSlideMarker();
                slide.Hidden = true;
                slide.DeleteIndicator();

                var previews = slide.GetShapesWithPrefix(PptLabsAgendaVisualItemPrefix);

                foreach (var preview in previews)
                {
                    var sectionName = preview.Name.Substring(PptLabsAgendaVisualItemPrefix.Length);
                    var captureName = string.Format("{0} Start.png", Common.FilenameBase64(sectionName));
                    preview.Fill.UserPicture(Path.Combine(SlideCapturePath, captureName));
                }
            } else
            {
                slide.MoveTo(slideIndex);
                slide.Design = refSlide.Design;
                if (relativeSectionIndex == sections.Count)
                {
                    AgendaSlide.SetSlideName(slide, Type.Visual, Purpose.EndOfVisualAgenda, string.Empty);
                }
                else
                {
                    AgendaSlide.SetSlideName(slide, Type.Visual, Purpose.VisualAgendaSection,
                        sections[relativeSectionIndex]);
                }
            }

            return slide;
        }

        private static void AddLinkBulletAgenda(PowerPointSlide slide)
        {
            slide.DeleteShapesWithPrefix(PptLabsAgendaBulletLinkShape);

            var contentHolder = slide.GetShapeWithName(PptLabsAgendaContentShapeName)[0];
            var textRange = contentHolder.TextFrame2.TextRange;

            for (var i = 1; i <= textRange.Paragraphs.Count; i++)
            {
                var curPara = textRange.Paragraphs[i];
                var boundBox = slide.Shapes.AddShape(MsoAutoShapeType.msoShapeRectangle,
                                                     curPara.BoundLeft, curPara.BoundTop,
                                                     curPara.BoundWidth, curPara.BoundHeight);
                var mouseOnClickAction = boundBox.ActionSettings[PpMouseActivation.ppMouseClick];

                mouseOnClickAction.Action = PpActionType.ppActionNamedSlideShow;
                mouseOnClickAction.Hyperlink.Address = null;
                mouseOnClickAction.Hyperlink.SubAddress = CreateInDocHyperLink(FindSectionStartSlide(curPara.Text, Type.None));

                boundBox.Name = PptLabsAgendaBulletLinkShape + curPara.Text.Trim();
                boundBox.Fill.Visible = MsoTriState.msoFalse;
                boundBox.Line.Visible = MsoTriState.msoFalse;
                boundBox.Visible = MsoTriState.msoFalse;
            }
        }

        private static void AddLinkVisualAgenda(PowerPointSlide slide)
        {
            var previews = slide.GetShapesWithPrefix(PptLabsAgendaVisualItemPrefix);
            var slides = PowerPointPresentation.Current.Slides;

            foreach (var preview in previews)
            {
                var sectionName = preview.Name.Substring(PptLabsAgendaVisualItemPrefix.Length);
                var secAbsoluteIndex = FindSectionAbsoluteIndex(sectionName);
                var secStartIndex = FindSectionStart(secAbsoluteIndex);
                var mouseOnClickAction = preview.ActionSettings[PpMouseActivation.ppMouseClick];

                mouseOnClickAction.Action = PpActionType.ppActionNamedSlideShow;
                mouseOnClickAction.Hyperlink.Address = null;
                mouseOnClickAction.Hyperlink.SubAddress = CreateInDocHyperLink(slides[secStartIndex - 2]);
            }
        }

        private static void AdjustBeamItemHorizontal(ref float lastLeft, ref float lastTop, ref float widest,
                                                     float delta, Shape item, Shape background)
        {
            if (lastLeft + delta > PowerPointPresentation.Current.SlideWidth)
            {
                lastLeft = 0;
                lastTop += item.Height;
            }

            item.Left = Math.Max(lastLeft, lastLeft + (delta - item.Width) / 2f);
            item.Top = lastTop;

            if (item.Width > widest)
            {
                widest = item.Width;
            }

            lastLeft += Math.Max(item.Width, delta);

            if (background.Height < lastTop + item.Height)
            {
                background.Height = lastTop + item.Height;
            }
        }

        private static void AdjustBeamItemVertical(ref float lastLeft, ref float lastTop, Shape item, Shape background)
        {
            lastTop += item.Height;

            if (lastTop >= PowerPointPresentation.Current.SlideHeight)
            {
                lastTop = 0;
                lastLeft += item.Width;

                item.Top = 0;
                item.Left = lastLeft;
            }

            if (background.Width < lastLeft + item.Width)
            {
                background.Width = lastLeft + item.Width;
            }
        }

        private static void AdjustBulletTemplateContent(int totalSection)
        {
            var refSlide = FindReferenceSlide(Type.Bullet);

            // post process bullet points
            var contentHolder = refSlide.GetShapeWithName(PptLabsAgendaContentShapeName)[0];
            var textRange = contentHolder.TextFrame2.TextRange;

            while (textRange.Paragraphs.Count < totalSection)
            {
                textRange.InsertAfter("\r ");
            }

            while (textRange.Paragraphs.Count > 3 && textRange.Paragraphs.Count > totalSection)
            {
                textRange.Paragraphs[textRange.Paragraphs.Count].Delete();
            }

            for (var i = 4; i <= textRange.Paragraphs.Count; i++)
            {
                textRange.Paragraphs[i].ParagraphFormat.Bullet.Type = MsoBulletType.msoBulletNone;
            }
        }

        private static void CheckAgendaUpdate(Type type, PowerPointSlide refSlide)
        {
            switch (type)
            {
                case Type.Beam:
                    CheckBeamAgendaUpdate(refSlide);
                    break;
                case Type.Bullet:
                    CheckBulletAgendaUpdate();
                    break;
                case Type.Visual:
                    CheckVisualAgendaUpdate(refSlide);
                    break;
            }
        }

        private static void CheckBeamAgendaUpdate(PowerPointSlide refSlide)
        {
            var beamShape = refSlide.GetShapeWithName(PptLabsAgendaBeamShapeName)[0];

            // check if the section names have changed
            var sections = PowerPointPresentation.Current.Sections.Skip(1).OrderBy(x => x).ToList();
            var beamItems = beamShape.GroupItems.Cast<Shape>()
                                                .Select(shape => shape.Name)
                                                .Where(name => name != PptLabsAgendaBeamBackgroundName)
                                                .OrderBy(x => x).ToList();

            for (var i = 0; i < beamItems.Count; i ++)
            {
                if (beamItems[i].EndsWith(PptLabsAgendaBeamHighlight))
                {
                    var totalLen = beamItems[i].Length;
                    var suffixLen = PptLabsAgendaBeamHighlight.Length;
                    beamItems[i] = beamItems[i].Remove(totalLen - suffixLen - 1, suffixLen + 1);
                }
            }

            _agendaOutdated = !sections.SequenceEqual(beamItems);
        }

        private static void CheckBulletAgendaUpdate()
        {
            var skippedSections = PowerPointPresentation.Current.Sections.Skip(1).ToList();
            var newSectionString = skippedSections.Aggregate((current, next) => current + "\r" + next) + "\r";
            var sections = skippedSections.OrderBy(x => x).ToArray();

            // TODO: check if the reference slide is at the very first

            if (_agendaText != null)
            {
                var oldSections = _agendaText.Trim().Split('\r').OrderBy(x => x);

                _agendaOutdated = !sections.SequenceEqual(oldSections);
            }

            if (_agendaText == null || _agendaOutdated)
            {
                _agendaOutdated = true;
                _agendaText = newSectionString;
            }
        }

        private static void CheckVisualAgendaUpdate(PowerPointSlide refSlide)
        {
            // delete all generated transition slides
            PowerPointPresentation.Current.RemoveSlide(
                AgendaSlide.MeetsConditions2(
                    slide =>
                        slide.SlidePurpose == Purpose.ZoomIn ||
                        slide.SlidePurpose == Purpose.ZoomOut), true);

            var visualItems = refSlide.GetShapesWithPrefix(PptLabsAgendaVisualItemPrefix).ToList();
            var sectionProperties = PowerPointPresentation.Current.SectionProperties;
            var sections = PowerPointPresentation.Current.Sections
                                                         .Skip(1)
                                                         .Where(section => section != PptLabsAgendaVisualSectionName)
                                                         .ToList();

            PrepareVisualAgendaSlideCapture(sections);

            foreach (var item in visualItems)
            {
                var sectionName = item.Name.Substring(PptLabsAgendaVisualItemPrefix.Length);
                var corresSection = sections.FirstOrDefault(section => section == sectionName);
                
                // remove outdated preview and corresponding agenda slide, and update current previews
                if (corresSection == null)
                {
                    item.Delete();

                    PowerPointPresentation.Current.RemoveSlide(
                                AgendaSlide.MeetsConditions2(
                                    slide => slide.AgendaType == Type.Visual && slide.Section == sectionName), false);
                } else
                {
                    sections.Remove(corresSection);
                    var captureName = string.Format("{0} Start.png", Common.FilenameBase64(sectionName));
                    item.Fill.UserPicture(Path.Combine(SlideCapturePath, captureName));
                }
            }

            if (sections.Count == 0) return;

            var itemWidth = visualItems[0].Width;
            var itemHeight = visualItems[0].Height;
            var itemLeft = 0f;
            var itemTop = 0f;
            var slideWidth = PowerPointPresentation.Current.SlideWidth;

            // process remaining sections, these are new sections
            foreach (var section in sections)
            {
                var newItem = refSlide.Shapes.AddShape(MsoAutoShapeType.msoShapeRectangle,
                                                       itemLeft, itemTop,
                                                       itemWidth, itemHeight);
                var captureName = string.Format("{0} Start.png", Common.FilenameBase64(section));
                newItem.Fill.UserPicture(Path.Combine(SlideCapturePath, captureName));

                itemLeft += itemWidth;

                if (itemLeft >= slideWidth)
                {
                    itemLeft = 0;
                    itemTop += itemHeight;
                }
            }

            var slides = PowerPointPresentation.Current.Slides;

            for (var i = 0; i < slides.Count; i ++)
            {
                if (AgendaSlide.IsAnyAgendaSlide(slides[i]))
                {
                    sectionProperties.AddBeforeSlide(i + 1, PptLabsAgendaVisualSectionName);
                }
            }
        }

        private static string CreateInDocHyperLink(PowerPointSlide slide)
        {
            return slide.ID + "," + slide.Index + "," + slide.Name;
        }

        private static Shape FindBeamHighlight(IEnumerable<Shape> beamItems)
        {
            return beamItems.FirstOrDefault(shape => shape.Name.EndsWith(PptLabsAgendaBeamHighlight));
        }

        private static Shape FindBeamNormal(IEnumerable<Shape> beamItems)
        {
            return beamItems.FirstOrDefault(shape => !shape.Name.EndsWith(PptLabsAgendaBeamHighlight) &&
                                                     shape.Name != PptLabsAgendaBeamBackgroundName);
        }

        private static Shape FindBeamShape(PowerPointSlide slide)
        {
            var grouped = slide.GetShapesWithTypeAndRule(MsoShapeType.msoGroup, new Regex(".+"));

            if (grouped == null || grouped.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < grouped.Count; i++)
            {
                if (Utils.Graphics.IsCorrupted(grouped[i]))
                {
                    grouped[i] = Utils.Graphics.CorruptionCorrection(grouped[i], slide);
                }
            }

            var beamShape = grouped.FirstOrDefault(shape => shape.Name == PptLabsAgendaBeamShapeName) ??
                            grouped.FirstOrDefault(shape => shape.GroupItems
                                                                 .Cast<Shape>()
                                                                 .Any(subItem => subItem.Name == PptLabsAgendaBeamBackgroundName));

            if (beamShape != null &&
                beamShape.Name != PptLabsAgendaBeamShapeName)
            {
                beamShape.Name = PptLabsAgendaBeamShapeName;
            }

            return beamShape;
        }

        private static PowerPointSlide FindReferenceSlide(Type type)
        {
            var slides = PowerPointPresentation.Current.Slides;
            if (type == Type.Beam)
            {
                return slides.FirstOrDefault(AgendaSlide.IsReferenceslide) ??
                       slides.FirstOrDefault(slide => slide.GetShapeWithName(PptLabsAgendaBeamShapeName).Count != 0);
            }
            else
            {
                return slides.FirstOrDefault(AgendaSlide.IsReferenceslide) ??
                       slides.FirstOrDefault(AgendaSlide.MeetsConditions(slide => slide.AgendaType == type));
            }
        }

        private static int FindSectionEnd(string section)
        {
            if (string.IsNullOrEmpty(section)) return -1;

            var sectionIndex = FindSectionAbsoluteIndex(section);

            return FindSectionEnd(sectionIndex);
        }

        private static int FindSectionEnd(int sectionIndex)
        {
            // take in absolute index!!!
            // here the sectionIndex is 1-based!
            var sectionProperties = PowerPointPresentation.Current.SectionProperties;

            return sectionProperties.FirstSlide(sectionIndex) + sectionProperties.SlidesCount(sectionIndex) - 1;
        }

        private static PowerPointSlide FindSectionEndSlide(string section, Type type)
        {
            // the function will return the end agenda slide if the first slide of the requested
            // section is an agenda slide, else it will return null. It also modify the name of the
            // end slide to adapt the section's name change.

            var curPresentation = PowerPointPresentation.Current;
            var slides = curPresentation.Slides;
            var sectionProperties = curPresentation.SectionProperties;
            var sectionIndex = FindSectionAbsoluteIndex(section);
            var endSlide = slides[sectionProperties.FirstSlide(sectionIndex) +
                                  sectionProperties.SlidesCount(sectionIndex) - 2];
            
            // return the slide immediately, don't need to be changed
            if (type == Type.Beam) return endSlide;

            if (AgendaSlide.IsAnyAgendaSlide(endSlide))
            {
                AgendaSlide.SetSlideName(endSlide, type, type == Type.Visual ? Purpose.None : Purpose.End, section);
            }
            else
            {
                endSlide = null;
            }

            return endSlide;
        }

        private static int FindSectionStart(string section)
        {
            if (string.IsNullOrEmpty(section)) return -1;

            var sectionIndex = FindSectionAbsoluteIndex(section);

            return FindSectionStart(sectionIndex);
        }

        private static int FindSectionStart(int sectionIndex)
        {
            // here the sectionIndex is 1-based!
            var sectionProperties = PowerPointPresentation.Current.SectionProperties;

            return sectionProperties.FirstSlide(sectionIndex);
        }

        private static PowerPointSlide FindSectionStartSlide(string section, Type type)
        {
            // the function will return the start agenda slide if the first slide of the requested
            // section is an agenda slide, else it will return null. It also modify the name of the
            // start slide to adapt the section's name change.
            var curPresentation = PowerPointPresentation.Current;
            var slides = curPresentation.Slides;
            var sectionProperties = curPresentation.SectionProperties;
            var sectionIndex = FindSectionAbsoluteIndex(section.Trim());

            if (sectionIndex < 1) return null;

            var startSlide = slides[sectionProperties.FirstSlide(sectionIndex) - 1];

            // if it's beam type or none type, return the slide immediately. None type should be
            // used if the user wants to return the first slide of each section regardless if
            // it's an agenda slide.
            if (type == Type.Beam || type == Type.None) return startSlide;

            if (AgendaSlide.IsAnyAgendaSlide(startSlide))
            {
                AgendaSlide.SetSlideName(startSlide, type, type == Type.Visual ? Purpose.None : Purpose.Start, section);
            }
            else
            {
                startSlide = null;
            }

            return startSlide;
        }

        private static int FindSectionAbsoluteIndex(string section)
        {
            if (string.IsNullOrEmpty(section)) return -1;

            // here the return value is 1-based!
            return PowerPointPresentation.Current.Sections.FindIndex(name => name == section) + 1;
        }

        private static string FindSlideSection(PowerPointSlide slide)
        {
            var sections = PowerPointPresentation.Current.Sections;

            for (var i = 0; i < sections.Count; i ++)
            {
                var sectionStart = FindSectionStart(sections[i]);
                
                if (sectionStart == slide.Index)
                {
                    return sections[i];
                }

                if (sectionStart > slide.Index)
                {
                    return sections[i - 1];
                }
            }

            return sections[sections.Count - 1];
        }

        /// <summary>
        /// Generates the beam agenda on the target slides. Skips over the Reference (Template) slide if included in targetSlides.
        /// Generates the Reference slide if it does not already exist.
        /// Leave the targetSlides field blank (=null) to generate the beam agenda over all slides (other than the first section).
        /// </summary>
        private static void GenerateBeamAgenda(List<string> sections, IEnumerable<PowerPointSlide> targetSlides = null)
        {
            List<PowerPointSlide> slides;
            if (targetSlides != null)
            {
                slides = targetSlides.Where(AgendaSlide.IsNotReferenceslide).ToList();
            }
            else
            {
                var firstSectionIndex = FindSectionStart(FindSectionAbsoluteIndex(sections[0]));
                slides = PowerPointPresentation.Current.Slides
                                                        .Where(slide => slide.Index >= firstSectionIndex && AgendaSlide.IsNotReferenceslide(slide))
                                                        .ToList();
            }

            if (slides.Count < 1) return;

            var refSlide = FindReferenceSlide(Type.Bullet);
            bool generateNewReferenceSlide;
            if (AgendaSlide.IsReferenceslide(refSlide))
            {
                var beamShape = FindBeamShape(refSlide);
                if (beamShape != null)
                {
                    generateNewReferenceSlide = false;
                    beamShape.Copy();
                }
                else
                {
                    // reference slide doesn't have a beam shape. weird. so we delete and recreate.
                    generateNewReferenceSlide = true;
                    refSlide.Delete();
                }
            }
            else
            {
                // can't find a reference slide.
                generateNewReferenceSlide = true;
            }

            if (generateNewReferenceSlide)
            {
                // if we do not have legacy template, create a new refslide 
                refSlide = PowerPointSlide.FromSlideFactory(PowerPointPresentation.Current
                                                                                .Presentation
                                                                                .Slides
                                                                                .Add(1, PpSlideLayout.ppLayoutBlank));
                AgendaSlide.SetAsReferenceSlideName(refSlide, Type.Beam);
                refSlide.AddTemplateSlideMarker();
                refSlide.Hidden = true;

                PrepareBeamAgendaShapes(sections, refSlide);
                AddAgendaSlideBeamType(sections[0], refSlide);
                refSlide.BringIndicatorToFront();
            }

            // The beam shape is now stored in the clipboard to be pasted on each of the slides.
            foreach (var slide in slides)
            {
                AddAgendaSlideBeamType(FindSlideSection(slide), slide);
            }
        }

        private static void GenerateBulletAgenda(List<string> sections)
        {
            _agendaText = TextCollection.AgendaLabReferenceSlideContent;

            for (var i = 4; i < sections.Count; i++)
            {
                _agendaText += "\r ";
            }

            var refSlide = FindReferenceSlide(Type.Bullet);

            if (AgendaSlide.IsNotReferenceslide(refSlide))
            {
                // if we do not have legacy template, create a new refslide 
                refSlide = AddAgendaSlideBulletType(string.Empty, false, null);
            }

            // here we invoke sync logic, since it's the same behavior as sync
            _agendaText = sections.Aggregate((current, next) => current + "\r" + next) + "\r";

            SyncAgendaBullet(sections, refSlide);
        }

        private static void GenerateVisualAgenda(List<string> sections)
        {
            PrepareVisualAgendaSlideCapture(sections);

            var refSlide = FindReferenceSlide(Type.Visual);
            
            if (AgendaSlide.IsNotReferenceslide(refSlide))
            {
                // if we do not have legacy template, create a new refslide 
                refSlide = AddAgendaSlideVisualType(sections, null, -1, -1);
            }

            SyncAgendaVisual(sections, refSlide);
        }

        private static void GenerateVisualAgendaSlideZoomIn(PowerPointSlide slide, Shape zoomInShape)
        {
            // add drill down effect and clean up current slide by deleting drill down
            // shape and recover original slide shape visibility
            PowerPointDrillDownSlide addedSlide;
            AutoZoom.AddDrillDownAnimation(zoomInShape, slide, out addedSlide);
            slide.GetShapesWithRule(new Regex("PPTZoomIn"))[0].Delete();
            string section = AgendaSlide.Decode(slide).Section;
            AgendaSlide.SetSlideName(addedSlide, Type.Visual, Purpose.ZoomIn, section);
            zoomInShape.Visible = MsoTriState.msoTrue;
        }

        private static void GenerateVisualAgendaSlideZoomOut(PowerPointSlide slide, Shape zoomOutShape)
        {
            // add step back effect  and clean up current slide by deleting step back
            // shape and recover original slide shape visibility
            PowerPointStepBackSlide addedSlide;
            AutoZoom.AddStepBackAnimation(zoomOutShape, slide, out addedSlide);
            slide.GetShapesWithRule(new Regex("PPTZoomOut"))[0].Delete();
            string section = AgendaSlide.Decode(slide).Section;
            AgendaSlide.SetSlideName(addedSlide, Type.Visual, Purpose.ZoomOut, section);
            zoomOutShape.Visible = MsoTriState.msoTrue;

            var index = slide.Index;

            // move the step back slide to the first slide of the section
            PowerPointPresentation.Current.Presentation.Slides[index - 1].MoveTo(index);
            slide.MoveTo(index);
        }

        private static void PickupBulletFormats()
        {
            var refSlide = FindReferenceSlide(Type.Bullet);
            var contentPlaceHolder = refSlide.GetShapeWithName(PptLabsAgendaContentShapeName)[0];
            var paragraphs = contentPlaceHolder.TextFrame2.TextRange
                                               .Paragraphs.Cast<TextRange2>().ToList();

            _bulletDimFormat = paragraphs[0];
            _bulletHighlightFormat = paragraphs[1];
            _bulletDefaultFormat = paragraphs[2];
        }

        private static Shape PrepareBeamAgendaBackground(PowerPointSlide slide, bool horizontal)
        {
            var background = slide.Shapes.AddShape(MsoAutoShapeType.msoShapeRectangle, 0, 0, 0, 0);
            background.Name = PptLabsAgendaBeamBackgroundName;
            background.Line.Visible = MsoTriState.msoFalse;
            background.Fill.ForeColor.RGB = Utils.Graphics.ConvertColorToRgb(Color.Black);

            if (horizontal)
            {
                background.Width = PowerPointPresentation.Current.SlideWidth;
            }
            else
            {
                background.Height = PowerPointPresentation.Current.SlideHeight;
            }

            return background;
        }

        private static Shape PrepareBeamAgendaBeamItem(PowerPointSlide slide, float lastLeft,
                                                       float lastTop, string section, bool enableButtons = false)
        {
            var textBox = slide.Shapes.AddTextbox(MsoTextOrientation.msoTextOrientationHorizontal,
                                                  lastLeft, lastTop, 0, 0);

            textBox.Name = section;
            textBox.TextFrame.AutoSize = PpAutoSize.ppAutoSizeShapeToFitText;
            textBox.TextFrame.WordWrap = MsoTriState.msoFalse;
            textBox.TextFrame.TextRange.Text = section;
            textBox.TextFrame.TextRange.Font.Color.RGB = Utils.Graphics.ConvertColorToRgb(Color.White);

            if (enableButtons)
            {
                var mouseOnClickAction = textBox.ActionSettings[PpMouseActivation.ppMouseClick];

                mouseOnClickAction.Action = PpActionType.ppActionNamedSlideShow;
                mouseOnClickAction.Hyperlink.Address = null;
                mouseOnClickAction.Hyperlink.SubAddress = CreateInDocHyperLink(FindSectionStartSlide(section, Type.Beam));
            }

            return textBox;
        }

        private static void PrepareBeamAgendaShapes(List<string> sections, PowerPointSlide refSlide)
        {
            var lastLeft = 0.0f;
            var lastTop = 0.0f;
            var slideWidth = PowerPointPresentation.Current.SlideWidth;
            var slideHeight = PowerPointPresentation.Current.SlideHeight;

            var horizontal = BeamDirection == Direction.Top || BeamDirection == Direction.Bottom;
            var background = PrepareBeamAgendaBackground(refSlide, horizontal);
            var widest = 0.0f;

            foreach (var section in sections)
            {
                var textBox = PrepareBeamAgendaBeamItem(refSlide, lastLeft, lastTop, section);

                if (horizontal)
                {
                    AdjustBeamItemHorizontal(ref lastLeft, ref lastTop, ref widest, 0, textBox, background);
                } else
                {
                    AdjustBeamItemVertical(ref lastLeft, ref lastTop, textBox, background);
                }
            }

            // for horizontal case, we need to evenly distribute every item
            if (horizontal)
            {
                background.Height = 0;
                lastLeft = 0;
                lastTop = 0;

                var delta = Math.Max(widest, slideWidth / sections.Count);

                foreach (var section in sections)
                {
                    var textBox = refSlide.GetShapeWithName(section)[0];

                    AdjustBeamItemHorizontal(ref lastLeft, ref lastTop, ref widest, delta, textBox, background);
                }
            }

            var copyRange = new List<string>();
            copyRange.AddRange(sections);
            copyRange.Add(PptLabsAgendaBeamBackgroundName);

            var group = refSlide.Shapes.Range(copyRange.ToArray()).Group();
            group.Name = PptLabsAgendaBeamShapeName;

            if (BeamDirection == Direction.Bottom)
            {
                group.Top = slideHeight - group.Height;
            } else
            if (BeamDirection == Direction.Right)
            {
                group.Left = slideWidth - group.Width;
            }

            group.Cut();
        }

        private static void PrepareSync(Type type, ref PowerPointSlide refSlide)
        {
            CheckAgendaUpdate(type, refSlide);
        }

        private static void PrepareVisualAgendaSlideCapture(IEnumerable<string> sections)
        {
            if (!Directory.Exists(SlideCapturePath))
            {
                Directory.CreateDirectory(SlideCapturePath);
            }

            var slides = PowerPointPresentation.Current.Slides;

            foreach (var section in sections)
            {
                var sectionStartSlide = slides[FindSectionStart(section) - 1];
                var sectionEndSlide = slides[FindSectionEnd(section) - 1];
                var animatedEndSlide = sectionEndSlide.Duplicate();

                foreach (var shape in animatedEndSlide.Shapes.Cast<Shape>().Where(animatedEndSlide.HasExitAnimation))
                {
                    shape.Delete();
                }

                animatedEndSlide.MoveMotionAnimation();

                var sectionStartName = string.Format("{0} Start.png", Common.FilenameBase64(section));
                var sectionEndName = string.Format("{0} End.png", Common.FilenameBase64(section));
                // TODO: BUG OCCURS HERE
                Utils.Graphics.ExportSlide(sectionStartSlide, Path.Combine(SlideCapturePath, sectionStartName));
                Utils.Graphics.ExportSlide(animatedEndSlide, Path.Combine(SlideCapturePath, sectionEndName));

                animatedEndSlide.Delete();
            }
        }

        private static void PrepareVisualAgendaSlideShapes(PowerPointSlide slide, List<string> sections)
        {
            var titleBar = slide.Shapes.Placeholders[1];

            titleBar.Name = PptLabsAgendaTitleShapeName;
            titleBar.TextFrame.TextRange.Text = "Agenda";

            var slideWidth = PowerPointPresentation.Current.SlideWidth;
            var slideHeight = PowerPointPresentation.Current.SlideHeight;
            var aspectRatio = slideWidth / slideHeight;
            var epsilon = slideHeight * 0.02f;

            var canvasLeft = titleBar.Left;
            var canvasTop = titleBar.Top + titleBar.Height + epsilon;
            var canvasWidth = titleBar.Width;
            var canvasHeight = canvasWidth / aspectRatio;

            var itemCount = sections.Count;
            var itemCanvasWidth = canvasWidth / itemCount;
            var itemWidth = itemCanvasWidth * (1 - 2 * VisualAgendaItemMargin);
            var itemHeight = itemWidth / aspectRatio;
            var itemTop = canvasTop + (canvasHeight - itemHeight) / 2;
            
            for (var i = 0; i < itemCount; i ++)
            {
                var itemLeft = canvasLeft + i*itemCanvasWidth + itemCanvasWidth * VisualAgendaItemMargin;

                var shape = slide.Shapes.AddShape(MsoAutoShapeType.msoShapeRectangle,
                                                  itemLeft, itemTop,
                                                  itemWidth, itemHeight);
                
                shape.Name = PptLabsAgendaVisualItemPrefix + sections[i];
                shape.Line.Visible = MsoTriState.msoFalse;
                shape.LockAspectRatio = MsoTriState.msoTrue;
            }
        }

        private static void ReformatTextRange(TextRange2 textRange, int focusIndex)
        {
            for (var i = 1; i <= textRange.Paragraphs.Count; i++)
            {
                var curPara = textRange.Paragraphs[i];

                if (i == focusIndex)
                {
                    Utils.Graphics.SyncTextRange(_bulletHighlightFormat, curPara, pickupTextContent: false);
                } else
                {
                    Utils.Graphics.SyncTextRange(i < focusIndex ? _bulletDimFormat : _bulletDefaultFormat, curPara,
                                                 pickupTextContent: false);
                }
            }
        }

        private static void RemoveBeamAgenda()
        {
            RemoveBeamAgendaFromSlides(PowerPointPresentation.Current.Slides);
            PowerPointPresentation.Current.RemoveSlide(AgendaSlide.IsAnyAgendaSlide, true);
        }

        private static void RemoveBeamAgendaFromSlides(IEnumerable<PowerPointSlide> candidates)
        {
            candidates = candidates.Where(AgendaSlide.IsNotReferenceslide);
            foreach (var candidate in candidates)
            {
                try
                {
                    var beamShape = FindBeamShape(candidate);

                    if (beamShape != null)
                    {
                        beamShape.Delete();
                    }
                }
                catch (Exception)
                {
                    // TODO: I cannot remember the reason for this empty catch block.....
                }
            }
        }

        private static void RemoveBulletAgenda()
        {
            PowerPointPresentation.Current.RemoveSlide(AgendaSlide.IsAnyAgendaSlide, true);
        }

        private static void RemoveVisualAgenda()
        {
            var curPresentation = PowerPointPresentation.Current;
            var sectionProperties = curPresentation.SectionProperties;
            var section = curPresentation.Sections;

            // to avoid accidentally deleting merged presentation slides, we could not
            // simply search for Visual Agenda Sections and delete them. Instead, we
            // delete all Visual Agenda Sections without deleting slides first, then
            // delete all transitions slides manually.

            // delete all generated sections without deleting the slides first
            for (var i = section.Count; i >= 1; i--)
            {
                if (section[i - 1] == PptLabsAgendaVisualSectionName)
                {
                    sectionProperties.Delete(i, false);
                }
            }

            // delete all transition slides
            PowerPointPresentation.Current.RemoveSlide(AgendaSlide.IsAnyAgendaSlide, true);
        }

        private static bool SectionValidation()
        {
            var sections = PowerPointPresentation.Current.Sections;

            if (sections.Count == 0)
            {
                MessageBox.Show(TextCollection.AgendaLabNoSectionError);
                return false;
            }

            if (sections.Count == 1)
            {
                MessageBox.Show(TextCollection.AgendaLabSingleSectionError);
                return false;
            }

            if (PowerPointPresentation.Current.HasEmptySection)
            {
                MessageBox.Show(TextCollection.AgendaLabEmptySectionError);
                return false;
            }

            if (HasDuplicateSectionName())
            {
                MessageBox.Show(TextCollection.AgendaLabDuplicateSectionError);
                return false;
            }

            return true;
        }

        private static void SelectOriginalSlide(PowerPointSlide oriSlide, PowerPointSlide defSlide)
        {
            if (oriSlide == null)
            {
                defSlide.GetNativeSlide().Select();
                return;
            }

            try
            {
                oriSlide.GetNativeSlide().Select();
            }
            catch (COMException)
            {
                if (defSlide != null)
                {
                    defSlide.GetNativeSlide().Select();
                }
            }
        }

        /// <summary>
        /// Synchronises the beam agenda by doing the following:
        /// 1) For all selected slides, if they do not already have the beam agenda on them, generate the beam agenda on them.
        /// 2) For all slides in the presentation with the beam agenda, sync it with the reference (template) slide.
        /// 
        /// Leave selectedSlides empty (null) if you only want to update slides.
        /// </summary>
        private static void SyncAgendaBeam(List<string> sections, PowerPointSlide refSlide, IEnumerable<PowerPointSlide> selectedSlides = null)
        {
            // Generate beam agenda for all selected slides that do not currently have the beam agenda.
            if (selectedSlides != null)
            {
                var slidesWithoutBeam = selectedSlides.Where(slide => FindBeamShape(slide) == null);
                GenerateBeamAgenda(sections, slidesWithoutBeam);
            }
            
            // Synchronise agenda for all slides in the presentation that have the beam agenda.
            var refBeamShape = FindBeamShape(refSlide);
            var allSlidesWithBeam = PowerPointPresentation.Current.Slides
                                                                  .Where(slide => AgendaSlide.IsNotReferenceslide(slide) &&
                                                                                  FindBeamShape(slide) != null);
            foreach (var slide in allSlidesWithBeam)
            {
                SyncSingleAgendaBeam(slide, refBeamShape);
            }
        }

        private static void SyncAgendaBullet(List<string> sections, PowerPointSlide refSlide, bool enableButtons = false)
        {
            AdjustBulletTemplateContent(sections.Count);

            PickupBulletFormats();

            for (var i = 0; i < sections.Count; i ++)
            {
                var section = sections[i];

                var start = FindSectionStartSlide(section, Type.Bullet);
                var end = FindSectionEndSlide(section, Type.Bullet);

                SyncSingleAgendaBullet(refSlide, start, section, false, i + 1);
                SyncSingleAgendaBullet(refSlide, end, section, true, i + 1);
            }

            if (enableButtons)
            {
                foreach (var section in sections)
                {
                    AddLinkBulletAgenda(FindSectionStartSlide(section, Type.Bullet));
                    AddLinkBulletAgenda(FindSectionEndSlide(section, Type.Bullet));
                }
            }
        }

        private static void SyncAgendaVisual(List<string> sections, PowerPointSlide refSlide, bool enableButtons = false)
        {
            // get a copy of slides and sections after the transition slides are deleted
            var sectionProperties = PowerPointPresentation.Current.SectionProperties;
            var slides = PowerPointPresentation.Current.Slides;
            var isGen = true;

            for (var i = sections.Count; i >= 0; i --)
            {
                var secAbsoluteIndex = i == sections.Count ? -1 : FindSectionAbsoluteIndex(sections[i]);
                var genSlideIndex = i == sections.Count
                                        ? PowerPointPresentation.Current.SlideCount + 1
                                        : FindSectionStart(secAbsoluteIndex);
                var genSectionIndex = i == sections.Count ? sectionProperties.Count : secAbsoluteIndex - 1;
                var genSectionName = sectionProperties.Name(genSectionIndex);

                PowerPointSlide candidate;

                if (genSectionName != PptLabsAgendaVisualSectionName && !isGen) continue;

                if (genSectionName == PptLabsAgendaVisualSectionName)
                {
                    candidate = sectionProperties.SlidesCount(genSectionIndex) == 0
                                    ? AddAgendaSlideVisualType(sections, refSlide, genSlideIndex, i)
                                    : slides[genSlideIndex - 2];
                    isGen = false;
                } else
                {
                    candidate = AddAgendaSlideVisualType(sections, refSlide, genSlideIndex, i);
                    sectionProperties.AddBeforeSlide(candidate.Index, PptLabsAgendaVisualSectionName);
                }

                SyncSingleAgendaGeneral(refSlide, candidate);
                SyncSingleAgendaVisual(candidate, sections, i);
            }

            var agendas = PowerPointPresentation.Current.Slides.Where(AgendaSlide.IsAnyAgendaSlide);

            if (enableButtons)
            {
                foreach (var agenda in agendas)
                {
                    AddLinkVisualAgenda(agenda);
                }
            }
        }

        private static void SyncSingleAgendaBeam(PowerPointSlide slide, Shape refBeamShape)
        {
            var refSubIems = refBeamShape.GroupItems.Cast<Shape>().ToList();
            var refHighlight = FindBeamHighlight(refSubIems);
            var refNormal = FindBeamNormal(refSubIems);
            var refBackground = refSubIems.FirstOrDefault(shape => shape.Name == PptLabsAgendaBeamBackgroundName);
            var beamShape = FindBeamShape(slide);

            if (beamShape == null) return;

            Utils.Graphics.SyncShape(refBeamShape, beamShape, pickupShapeFormat: false);

            var subItems = beamShape.GroupItems.Cast<Shape>().ToList();
            var section = FindSlideSection(slide);
            var widest = 0f;

            foreach (var item in subItems)
            {
                // specially deal with the background shape
                if (item.Name == PptLabsAgendaBeamBackgroundName)
                {
                    Utils.Graphics.SyncShape(refBackground, item, pickupTextContent: false);

                    if (_agendaOutdated)
                    {
                        item.Width = PowerPointPresentation.Current.SlideWidth;
                    }
                }
                else
                {
                    // for all items, we have 2 cases:
                    // 1. if the sections have not been changed, we follow the reference for the layout and format;
                    // 2. if the sections have been changed, we follow only the format, but not the layout
                    var itemText = item.TextFrame.TextRange.Text;

                    if (!_agendaOutdated)
                    {
                        var oldItem = refSubIems.FirstOrDefault(shape => shape.TextFrame.TextRange.Text == itemText);

                        // pick up layout first
                        Utils.Graphics.SyncShape(oldItem, item, pickupShapeFormat: false,
                                                 pickupTextContent: false, pickupTextFormat: false);
                    }

                    Utils.Graphics.SyncShape(itemText == section ? refHighlight : refNormal, item,
                                             pickupShapeBasic: false, pickupTextContent: false);

                    widest = Math.Max(widest, item.Width);
                }
            }

            if (_agendaOutdated)
            {
                var lastLeft = 0f;
                var lastTop = 0f;

                var delta = Math.Max(widest, PowerPointPresentation.Current.SlideWidth / (subItems.Count - 1));
                var background = subItems.FirstOrDefault(shape => shape.Name == PptLabsAgendaBeamBackgroundName);

                if (background != null)
                {
                    background.Height = 0;
                }

                foreach (var item in subItems.Where(shape => shape.Name != PptLabsAgendaBeamBackgroundName))
                {
                    // TODO: adjust previous line when lastTop increases
                    AdjustBeamItemHorizontal(ref lastLeft, ref lastTop, ref widest, delta, item, background);
                }
            }
        }

        private static void SyncSingleAgendaBullet(PowerPointSlide refSlide, PowerPointSlide candidate,
                                                   string section, bool isEnd, int focusIndex)
        {
            // if this is a new section, we need to generate a new agenda slide, else we need to check
            // if the slide's content is outdated. If so, we need to update the content and reformat it
            // according to the refslide.
            var refContentHolder = refSlide.GetShapeWithName(PptLabsAgendaContentShapeName)[0];
            var contentHolder = (candidate ?? (candidate = AddAgendaSlideBulletType(section, isEnd, refSlide))).GetShapeWithName(PptLabsAgendaContentShapeName)[0];

            if (_agendaOutdated)
            {
                contentHolder.TextFrame.TextRange.Text = _agendaText;
            }

            // after syncing the content, we need to take care of the general slide settings
            SyncSingleAgendaGeneral(refSlide, candidate);

            // then we sync the content holder without modifying the content
            Utils.Graphics.SyncShape(refContentHolder, contentHolder,
                                     pickupTextContent: false, pickupTextFormat: false);

            // finally recolor the bullets
            ReformatTextRange(contentHolder.TextFrame2.TextRange, focusIndex);
        }

        private static bool HasDuplicateSectionName()
        {
            var sections = PowerPointPresentation.Current.Sections.Where(name => name != PptLabsAgendaVisualSectionName);
            var names = new HashSet<string>();
            foreach (var section in sections)
            {
                if (names.Contains(section))
                {
                    return true;
                }
                names.Add(section);
            }
            return false;
        }

        private static void SyncSingleAgendaVisual(PowerPointSlide candidate, List<string> sections, int sectionIndex)
        {
            // sections contains the meaningful sections in this presentation
            // sectionIndex here is the index of current section in meaningful sections
            var shapes = candidate.GetShapesWithPrefix(PptLabsAgendaVisualItemPrefix).ToList();

            foreach (var shape in shapes)
            {
                var sectionName = shape.Name.Substring(PptLabsAgendaVisualItemPrefix.Length);

                // if this shape is outdated, we should remove it
                if (sections.All(name => name != sectionName))
                {
                    shape.Delete();
                } else
                {
                    var index = sections.FindIndex(section => section == sectionName);
                    var captureName = string.Format("{0} {1}.png", Common.FilenameBase64(sectionName), index < sectionIndex ? "End" : "Start");
                    shape.Fill.UserPicture(Path.Combine(SlideCapturePath, captureName));

                    if (sectionIndex < sections.Count && index == sectionIndex)
                    {
                        GenerateVisualAgendaSlideZoomIn(candidate, shape);
                    }

                    if (sectionIndex > 0 && index == sectionIndex - 1)
                    {
                        GenerateVisualAgendaSlideZoomOut(candidate, shape);
                    }
                }
            }
        }

        private static void SyncSingleAgendaGeneral(PowerPointSlide refSlide, PowerPointSlide candidate)
        {
            if (refSlide == null || candidate == null ||
                refSlide == candidate)
            {
                return;
            }

            candidate.Layout = refSlide.Layout;
            candidate.Design = refSlide.Design;

            // syncronize extra shapes other than visual items in reference slide
            var extraShapes = refSlide.Shapes.Cast<Shape>()
                                             .Where(shape => !candidate.HasShapeWithSameName(shape.Name) &&
                                                             !shape.Name.Contains(PptLabsAgendaVisualItemPrefix) &&
                                                             !PowerPointSlide.IsIndicator(shape) &&
                                                             !PowerPointSlide.IsTemplateSlideMarker(shape))
                                             .Select(shape => shape.Name)
                                             .ToArray();

            if (extraShapes.Length != 0)
            {
                var refShapes = refSlide.Shapes.Range(extraShapes);
                refShapes.Copy();
                var copiedShapes = candidate.Shapes.Paste();

                Utils.Graphics.SyncShapeRange(refShapes, copiedShapes);
            }

            // syncronize shapes position and size, except bullet content
            var sameShapes = refSlide.Shapes.Cast<Shape>()
                                            .Where(shape => shape.Name != PptLabsAgendaContentShapeName &&
                                                            !PowerPointSlide.IsIndicator(shape) &&
                                                            !PowerPointSlide.IsTemplateSlideMarker(shape) &&
                                                            candidate.HasShapeWithSameName(shape.Name));

            foreach (var refShape in sameShapes)
            {
                var candidateShape = candidate.GetShapeWithName(refShape.Name)[0];

                Utils.Graphics.SyncShape(refShape, candidateShape);
            }
        }
        # endregion

        # region Event Handlers
        public static void SlideShowBeginHandler()
        {
            var type = CurrentType;

            if (type != Type.Bullet) return;

            var slides = PowerPointPresentation.Current.Slides.Where(AgendaSlide.IsAnyAgendaSlide);

            foreach (var slide in slides)
            {
                var linkShapes = slide.GetShapesWithPrefix(PptLabsAgendaBulletLinkShape);
                var contentHolder = slide.GetShapeWithName(PptLabsAgendaContentShapeName)[0];
                var textRange = contentHolder.TextFrame2.TextRange;

                if (linkShapes.Count == 0) return;

                for (var i = 1; i <= textRange.Paragraphs.Count; i++)
                {
                    var shape = linkShapes[i - 1];
                    var curPara = textRange.Paragraphs[i];

                    shape.Left = curPara.BoundLeft;
                    shape.Top = curPara.BoundTop;
                    shape.Width = curPara.BoundWidth;
                    shape.Height = curPara.BoundHeight;

                    shape.Visible = MsoTriState.msoTrue;
                }
            }
        }

        public static void SlideShowEndHandler()
        {
            var type = CurrentType;

            if (type != Type.Bullet) return;

            var slides = PowerPointPresentation.Current.Slides.Where(AgendaSlide.IsAnyAgendaSlide);

            foreach (var slide in slides)
            {
                var linkShapes = slide.GetShapesWithPrefix(PptLabsAgendaBulletLinkShape);

                foreach (var linkShape in linkShapes)
                {
                    linkShape.Visible = MsoTriState.msoFalse;
                }
            }
        }
        # endregion
    }
}
