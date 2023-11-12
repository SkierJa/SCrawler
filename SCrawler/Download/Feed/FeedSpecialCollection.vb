﻿' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports PersonalUtilities.Tools
Imports PersonalUtilities.Forms
Namespace DownloadObjects
    Friend Class FeedSpecialCollection : Implements IEnumerable(Of FeedSpecial), IMyEnumerator(Of FeedSpecial)
#Region "Events"
        Friend Delegate Sub FeedActionEventHandler(ByVal Source As FeedSpecialCollection, ByVal Feed As FeedSpecial)
        Friend Event FeedAdded As FeedActionEventHandler
        Friend Event FeedRemoved As FeedActionEventHandler
#End Region
#Region "FeedsComparer"
        Private Class FeedsComparer : Implements IComparer(Of FeedSpecial)
            Friend Function Compare(ByVal x As FeedSpecial, ByVal y As FeedSpecial) As Integer Implements IComparer(Of FeedSpecial).Compare
                If x.IsFavorite Then
                    Return -1
                Else
                    Return x.Name.CompareTo(y.Name)
                End If
            End Function
        End Class
#End Region
#Region "Declarations"
        Private ReadOnly Feeds As List(Of FeedSpecial)
        Private _Favorite As FeedSpecial = Nothing
        Friend ReadOnly Property Favorite As FeedSpecial
            Get
                If _Favorite Is Nothing Then _Favorite = FeedSpecial.CreateFavorite : _Favorite.Load()
                Return _Favorite
            End Get
        End Property
        Private _Loaded As Boolean = False
        Friend ReadOnly Property Comparer As New FeedSpecial.SEComparer
        Private ReadOnly Property ComparerFeeds As New FeedsComparer
        Friend ReadOnly Property FeedSpecialRemover As Predicate(Of SFile) = Function(f) f.Name.StartsWith(FeedSpecial.FavoriteName) Or f.Name.StartsWith(FeedSpecial.SpecialName)
#End Region
#Region "Initializer, loader, feeds handlers"
        Friend Sub New()
            Feeds = New List(Of FeedSpecial)
        End Sub
        Friend Sub Load()
            Try
                If Not _Loaded Then
                    _Loaded = True
                    Dim files As List(Of SFile) = SFile.GetFiles(TDownloader.SessionsPath, $"{FeedSpecial.FavoriteName}.xml|{FeedSpecial.SpecialName}_*.xml",, EDP.ReturnValue)
                    If files.ListExists Then files.ForEach(Sub(f)
                                                               Feeds.Add(New FeedSpecial(f))
                                                               With Feeds.Last
                                                                   If .IsFavorite Then _Favorite = .Self
                                                                   AddHandler .FeedDeleted, AddressOf Feeds_FeedDeleted
                                                               End With
                                                           End Sub) : files.Clear()
                End If
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, "[FeedSpecialCollection.Load]")
                MainFrameObj.UpdateLogButton()
            End Try
        End Sub
        Private Sub Feeds_FeedDeleted(ByVal Source As FeedSpecialCollection, ByVal Feed As FeedSpecial)
            RaiseEvent FeedRemoved(Me, Feed)
            If Count > 0 Then Feeds.Remove(Feed)
        End Sub
#End Region
#Region "ChooseFeeds"
        Friend Shared Function ChooseFeeds(ByVal AllowAdd As Boolean) As List(Of FeedSpecial)
            Try
                Dim newFeed$ = String.Empty
                Using f As New SimpleListForm(Of String)(Settings.Feeds.Select(Function(ff) ff.Name), Settings.Design) With {
                    .DesignXMLNodeName = "FeedsChooserForm",
                    .Icon = My.Resources.RSSIcon_32,
                    .FormText = "Feeds"
                }
                    If AllowAdd Then f.AddFunction = Sub(ByVal sender As Object, ByVal e As SimpleListFormEventArgs)
                                                         If newFeed.IsEmptyString Then
                                                             Dim nf$ = InputBoxE("Enter a new feed name:", "New feed")
                                                             If Not nf.IsEmptyString Then
                                                                 If Settings.Feeds.ListExists(Function(ff) ff.Name.StringToLower = nf.ToLower) Then
                                                                     MsgBoxE({$"A feed named '{nf}' already exists", "New feed"}, vbCritical)
                                                                 Else
                                                                     newFeed = nf
                                                                     e.Item = nf
                                                                 End If
                                                             Else
                                                                 MsgBoxE({"You can only create one feed at a time", "New feed"}, vbCritical)
                                                             End If
                                                         End If
                                                     End Sub
                    If f.ShowDialog = DialogResult.OK AndAlso f.DataResult.Count > 0 Then
                        If Not newFeed.IsEmptyString AndAlso f.DataResult.Contains(newFeed) Then Settings.Feeds.Add(newFeed)
                        Return Settings.Feeds.Where(Function(ff) f.DataResult.Contains(ff.Name)).ToList
                    End If
                End Using
                Return Nothing
            Catch ex As Exception
                Return ErrorsDescriber.Execute(EDP.SendToLog, ex, "[FeedSpecialCollection.ChooseFeeds]")
            End Try
        End Function
#End Region
#Region "Item, Count"
        Default Friend ReadOnly Property Item(ByVal Index As Integer) As FeedSpecial Implements IMyEnumerator(Of FeedSpecial).MyEnumeratorObject
            Get
                Return Feeds(Index)
            End Get
        End Property
        Friend ReadOnly Property Count As Integer Implements IMyEnumerator(Of FeedSpecial).MyEnumeratorCount
            Get
                Return Feeds.Count
            End Get
        End Property
#End Region
#Region "Add, Delete"
        Friend Function Add(ByVal Name As String) As Integer
            Dim i% = -1
            If Not Name.IsEmptyString Then
                If Count = 0 Then
                    Feeds.Add(FeedSpecial.CreateSpecial(Name))
                    Feeds.Last.Save()
                    i = Count - 1
                Else
                    i = Feeds.FindIndex(Function(f) f.Name = Name)
                    If i = -1 Then
                        Feeds.Add(FeedSpecial.CreateSpecial(Name))
                        Feeds.Last.Save()
                        i = Count - 1
                    End If
                End If
            End If
            If i >= 0 Then
                Feeds.Sort(ComparerFeeds)
                i = Feeds.FindIndex(Function(f) f.Name = Name)
                If i >= 0 Then RaiseEvent FeedAdded(Me, Feeds(i))
            End If
            Return i
        End Function
        Friend Function Delete(ByVal Item As FeedSpecial) As Boolean
            Dim result As Boolean = False
            Dim i% = Feeds.IndexOf(Item)
            If i >= 0 Then
                With Feeds(i)
                    If .IsFavorite Then
                        result = .Clear
                    Else
                        result = .Delete
                        If result Then
                            .Dispose()
                            Feeds.RemoveAt(i)
                        End If
                    End If
                End With
            End If
            Return result
        End Function
#End Region
#Region "IEnumerable Support"
        Private Function GetEnumerator() As IEnumerator(Of FeedSpecial) Implements IEnumerable(Of FeedSpecial).GetEnumerator
            Return New MyEnumerator(Of FeedSpecial)(Me)
        End Function
        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return GetEnumerator()
        End Function
#End Region
    End Class
End Namespace