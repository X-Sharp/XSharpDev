//
// Copyright (c) XSharp B.V.  All Rights Reserved.
// Licensed under the Apache License, Version 2.0.
// See License.txt in the project root for license information.
//

lexer grammar XSharpLexer;

/*
 * Lexer Rules
*/

options	{
		}


channels {
			XMLDOCCHANNEL,
			DEFOUTCHANNEL,
			PREPROCESSORCHANNEL
		}

tokens {

// Keywords
// Old (VO) Keywords can have 4 letter abbreviations. This can be enabled/disabled with the
// AllowFourLetterAbbreviations property of the Lexer, which sets the protected field _Four.
// New (Vulcan) Keywords only full names
//
FIRST_KEYWORD,
ACCESS,ALIGN,AS,ASPEN,ASSIGN,BEGIN,BREAK,CALLBACK,CASE,CAST,CLASS,CLIPPER,DECLARE,DEFINE,DIM,DLL,DLLEXPORT,DO,DOWNTO,ELSE,ELSEIF,END,ENDCASE,ENDDO,ENDIF,EXIT,EXPORT,FASTCALL,FIELD,
FOR,FUNCTION,GLOBAL,HIDDEN,IF,IIF,INHERIT,INIT1,INIT2,INIT3,INSTANCE,IS,IN,LOCAL,LOOP,MEMBER,MEMVAR,METHOD,NAMEOF,NEXT,OTHERWISE,PARAMETERS,PASCAL,
PRIVATE,PROCEDURE,PROTECTED,PUBLIC,RECOVER,RETURN,SELF,SEQUENCE,SIZEOF,STATIC,STEP,STRICT,SUPER,THISCALL,TO,TYPEOF,UNION,
UPTO,USING,WHILE,WINCALL,

// Vulcan keywords that are not part of the identifier rule
// to prevent parser disambiguities
// (These keywords were NOT contextual in Vulcan either)
//
CATCH,FINALLY,THROW,


FIRST_POSITIONAL_KEYWORD,
// New Vulcan Keywords (no 4 letter abbreviations)
// Should also all be part of the identifier rule
//
ABSTRACT,AUTO,CASTCLASS,CONSTRUCTOR,CONST,DEFAULT,DELEGATE,DESTRUCTOR,ENUM,EVENT,EXPLICIT,FOREACH,GET,IMPLEMENTS,IMPLICIT,IMPLIED,INITONLY,INTERFACE,INTERNAL,
LOCK,NAMESPACE,NEW,OPERATOR,OUT,PARTIAL,PROPERTY,REPEAT,SCOPE,SEALED,SET,STRUCTURE,TRY,UNTIL,VALUE,VIRTUAL,VOSTRUCT,


// New XSharp Keywords (no 4 letter abbreviations)
// Should also all be part of the identifier rule
//
ADD,ARGLIST,ASCENDING,ASYNC,ASTYPE,AWAIT,BY,CHECKED,DESCENDING,EQUALS,EXTERN,FIXED,FROM,GROUP,INTO,JOIN,LET,NOP,OF,ON,ORDERBY,OVERRIDE,PARAMS,
REMOVE,SELECT,SWITCH, UNCHECKED,UNSAFE,VAR,VOLATILE,WHEN,WHERE,YIELD,
WITH,
LAST_POSITIONAL_KEYWORD,

// Predefined types
FIRST_TYPE,
ARRAY,BYTE,CODEBLOCK,DATE,DWORD,FLOAT,INT,LOGIC,LONGINT,OBJECT,PSZ,PTR,REAL4,REAL8,REF,SHORTINT,STRING,SYMBOL,USUAL,VOID,WORD,

// Vulcan Types
CHAR,INT64,UINT64,

// XSharp Types
DYNAMIC, DECIMAL, DATETIME, CURRENCY,BINARY,
LAST_TYPE,

// UDC Tokens that should be shown in the keyword color
UDC_KEYWORD,

// Scripting directives (pseudo-preprocessor handling)
SCRIPT_REF, SCRIPT_LOAD,

// XPP Keywords
ASSIGNMENT, DEFERRED, ENDCLASS, EXPORTED, FREEZE, FINAL, INLINE, INTRODUCE, NOSAVE, READONLY, SHARING, SHARED, SYNC,  


// FoxPro Keywords
ENDDEFINE, LPARAMETERS, OLEPUBLIC, EXCLUDE, THISACCESS, HELPSTRING, DIMENSION,NOINIT,EACH,THEN,FOX_M, 
// Text .. endText
TEXT, ENDTEXT, ADDITIVE, FLAGS, PRETEXT, NOSHOW, TEXTMERGE,

LAST_KEYWORD,

// Null values
FIRST_NULL,

NIL,NULL,NULL_ARRAY,NULL_CODEBLOCK,NULL_DATE,NULL_OBJECT,NULL_PSZ,NULL_PTR,NULL_STRING,NULL_SYMBOL,

LAST_NULL,

// Relational operators
FIRST_OPERATOR,
LT,LTE,GT,GTE,EQ,EEQ,SUBSTR,NEQ,NEQ2,

// Prefix and postfix Operators
INC,DEC,

// Unary & binary operators
PLUS,MINUS,DIV,MOD,EXP,LSHIFT,RSHIFT,TILDE,MULT,QQMARK,QMARK, 

// Boolean operators
AND,OR,NOT,

// VO Bitwise operators
VO_NOT, VO_AND, VO_OR, VO_XOR,

// Assignments
ASSIGN_OP,ASSIGN_ADD,ASSIGN_SUB,ASSIGN_EXP,ASSIGN_MUL,ASSIGN_DIV,
ASSIGN_MOD,ASSIGN_BITAND,ASSIGN_BITOR,ASSIGN_LSHIFT,ASSIGN_RSHIFT,
ASSIGN_XOR,


// Operators
LOGIC_AND,LOGIC_OR,LOGIC_NOT,LOGIC_XOR,FOX_AND, FOX_OR, FOX_NOT,FOX_XOR,

// Symbols
LPAREN,RPAREN,LCURLY,RCURLY,LBRKT,RBRKT,COLON,COMMA,PIPE,AMP,ADDROF,ALIAS,DOT,COLONCOLON,BACKSLASH,ELLIPSIS,BACKBACKSLASH,

LAST_OPERATOR,

FIRST_CONSTANT,
// Logics
FALSE_CONST,TRUE_CONST, 
// Consts
HEX_CONST,BIN_CONST,INT_CONST,DATE_CONST,DATETIME_CONST,REAL_CONST,INVALID_NUMBER,
SYMBOL_CONST,CHAR_CONST,STRING_CONST,ESCAPED_STRING_CONST,INTERPOLATED_STRING_CONST,INCOMPLETE_STRING_CONST,TEXT_STRING_CONST,BRACKETED_STRING_CONST,
BINARY_CONST,
LAST_CONSTANT,

// Pre processor symbols
PP_FIRST,
PP_COMMAND,PP_DEFINE,PP_ELSE,PP_ENDIF,PP_ENDREGION,PP_ERROR,PP_IFDEF,PP_IFNDEF,PP_INCLUDE,PP_LINE,PP_REGION,PP_TRANSLATE,
PP_UNDEF,PP_WARNING,
PP_LAST,

// PP constant
MACRO,	  // __term__
UDCSEP, // =>

// Ids
ID,KWID,

// Pragma
PRAGMA, 

// Comments
DOC_COMMENT,SL_COMMENT,ML_COMMENT,

// Separators
LINE_CONT,LINE_CONT_OLD,
SEMI,WS,NL,EOS,

// Error
UNRECOGNIZED

}

/*
 * Lexer Rules
 */

UNRECOGNIZED	: . ;
