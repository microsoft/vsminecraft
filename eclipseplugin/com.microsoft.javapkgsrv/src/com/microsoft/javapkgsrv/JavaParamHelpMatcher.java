// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgsrv;

import org.eclipse.jface.text.Assert;
import org.eclipse.jface.text.BadLocationException;
import org.eclipse.jface.text.IDocument;
import org.eclipse.jface.text.IRegion;
import org.eclipse.jface.text.Region;

@SuppressWarnings("deprecation")
public class JavaParamHelpMatcher {
    private class CharPairs {

        private final char[] fPairs;

        public CharPairs(char[] pairs) {
            fPairs = pairs;
        }

        /**
         * Returns true if the specified character occurs in one of the character pairs.
         *
         * @param c a character
         * @return true exactly if the character occurs in one of the pairs
         */
        public boolean contains(char c) {
            char[] pairs = fPairs;
            for (int i = 0, n = pairs.length; i < n; i++) {
                if (c == pairs[i]) {
                    return true;
                }
            }
            return false;
        }

        /**
         * Returns true if the specified character opens a character pair
         * when scanning in the specified direction.
         *
         * @param c             a character
         * @param searchForward the direction of the search
         * @return whether or not the character opens a character pair
         */
        public boolean isOpeningCharacter(char c, boolean searchForward) {
            for (int i = 0; i < fPairs.length; i += 2) {
                if (searchForward && getStartChar(i) == c) {
                    return true;
                } else if (!searchForward && getEndChar(i) == c) {
                    return true;
                }
            }
            return false;
        }

        /**
         * Returns true if the specified character is a start character.
         *
         * @param c a character
         * @return true exactly if the character is a start character
         */
        public boolean isStartCharacter(char c) {
            return this.isOpeningCharacter(c, true);
        }

        /**
         * Returns true if the specified character is an end character.
         *
         * @param c a character
         * @return true exactly if the character is an end character
         * @since 3.8
         */
        public boolean isEndCharacter(char c) {
            return this.isOpeningCharacter(c, false);
        }

        /**
         * Returns the matching character for the specified character.
         *
         * @param c a character occurring in a character pair
         * @return the matching character
         */
        public char getMatching(char c) {
            for (int i = 0; i < fPairs.length; i += 2) {
                if (getStartChar(i) == c) {
                    return getEndChar(i);
                } else if (getEndChar(i) == c) {
                    return getStartChar(i);
                }
            }
            Assert.isTrue(false);
            return '\0';
        }

        private char getStartChar(int i) {
            return fPairs[i];
        }

        private char getEndChar(int i) {
            return fPairs[i + 1];
        }

    }

    private CharPairs fPairs;
    private char[] fSeparators;

    private boolean isSeparator(char c) {
        for (int i = 0, n = fSeparators.length; i < n; i++) {
            if (c == fSeparators[i]) {
                return true;
            }
        }
        return false;
    }

    public JavaParamHelpMatcher(char[] chars, char[] separators) {
        fPairs = new CharPairs(chars);
        fSeparators = separators;
    }

    private class DocumentAccessor {
        private IDocument fDocument;

        public DocumentAccessor(IDocument document) {
            fDocument = document;
        }

        public char getChar(int pos) throws BadLocationException {
            return fDocument.getChar(pos);
        }

        public int getNextPosition(int currentPos, boolean searchForward) {
            return currentPos + (searchForward ? 1 : -1);
        }

        public boolean inPartition(int pos) {
            return true;
        }
    }

    public class ParamRegion {
        public IRegion region = null;
        public int paramSeparatorCount = 0;
    }

    public ParamRegion findEnclosingPeerCharacters(IDocument document, int offset, int length) {
        if (document == null || offset < 0 || offset > document.getLength())
            return null;
        try {
            DocumentAccessor doc = new DocumentAccessor(document);
            return findEnclosingPeers(document, doc, offset, length, 0, document.getLength());
        } catch (BadLocationException ble) {
            return null;
        }
    }

    private ParamRegion findEnclosingPeers(IDocument document, DocumentAccessor doc, int offset, int length, int lowerBoundary, int upperBoundary) throws BadLocationException {
        char[] pairs = fPairs.fPairs;
        /* Special ParamHelp added here */
        int cSeparators = 0;
        /* Special ParamHelp end here */

        int start;
        int end;
        if (length >= 0) {
            start = offset;
            end = offset + length;
        } else {
            end = offset;
            start = offset + length;
        }

        boolean lowerFound = false;
        boolean upperFound = false;
        int[][] counts = new int[pairs.length][2];
        char currChar = (start != document.getLength()) ? doc.getChar(start) : Character.MIN_VALUE;
        int pos1;
        int pos2;
        if (fPairs.isEndCharacter(currChar)) {
            pos1 = doc.getNextPosition(start, false);
            pos2 = start;
        }
        /* Special ParamHelp added here */
        else if (isSeparator(currChar)) {
            pos1 = doc.getNextPosition(start, false);
            pos2 = doc.getNextPosition(start, true);
        }
        /* Special ParamHelp end here */
        else {
            pos1 = start;
            pos2 = doc.getNextPosition(start, true);
        }

        while ((pos1 >= lowerBoundary && !lowerFound) || (pos2 < upperBoundary && !upperFound)) {
            for (int i = 0; i < counts.length; i++) {
                counts[i][0] = counts[i][1] = 0;
            }

            outer1:
            while (pos1 >= lowerBoundary && !lowerFound) {
                final char c = doc.getChar(pos1);
                int i = getCharacterIndex(c, document, pos1);
                if (i != -1 && doc.inPartition(pos1)) {
                    if (i % 2 == 0) {
                        counts[i / 2][0]--; //start
                    } else {
                        counts[i / 2][0]++; //end
                    }
                    for (int j = 0; j < counts.length; j++) {
                        if (counts[j][0] == -1) {
                            lowerFound = true;
                            break outer1;
                        }
                    }
                }
                /* Special ParamHelp added here */
                else if (isSeparator(c)) {
                    boolean nestedSeparator = false;
                    for (int j = 0; j < counts.length; j++) {
                        if (counts[j][0] != 0) {
                            nestedSeparator = true;
                        }
                    }
                    if (!nestedSeparator)
                        cSeparators++;

                }
                /* Special ParamHelp end here */
                pos1 = doc.getNextPosition(pos1, false);
            }

            outer2:
            while (pos2 < upperBoundary && !upperFound) {
                final char c = doc.getChar(pos2);
                int i = getCharacterIndex(c, document, pos2);
                if (i != -1 && doc.inPartition(pos2)) {
                    if (i % 2 == 0) {
                        counts[i / 2][1]++; //start
                    } else {
                        counts[i / 2][1]--; //end
                    }
                    for (int j = 0; j < counts.length; j++) {
                        if (counts[j][1] == -1 && counts[j][0] == -1) {
                            upperFound = true;
                            break outer2;
                        }
                    }
                }
                pos2 = doc.getNextPosition(pos2, true);
            }

            if (pos1 > start || pos2 < end - 1) {
                //match inside selection => discard
                pos1 = doc.getNextPosition(pos1, false);
                pos2 = doc.getNextPosition(pos2, true);
                lowerFound = false;
                upperFound = false;
            }
        }
        pos2++;
        if (pos1 < lowerBoundary || pos2 > upperBoundary)
            return null;
        ParamRegion ret = new ParamRegion();
        ret.region = new Region(pos1, pos2 - pos1);
        ret.paramSeparatorCount = cSeparators;
        return ret;
    }

    private int getCharacterIndex(char ch, IDocument document, int offset) {
        char[] pairs = fPairs.fPairs;
        for (int i = 0; i < pairs.length; i++) {
            if (pairs[i] == ch && isMatchedChar(ch, document, offset)) {
                return i;
            }
        }
        return -1;
    }

    public boolean isMatchedChar(char ch, IDocument document, int offset) {
        return isMatchedChar(ch);
    }

    public boolean isMatchedChar(char ch) {
        return fPairs.contains(ch);
    }
}
