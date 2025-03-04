/*
MIT LICENSE

Copyright (c) 2024 Webnames.ca Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy 
of this software and associated documentation files (the “Software”), to deal 
in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the Software is furnished
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
THE SOFTWARE.
*/
// MailSPEC CommuniGate Pro CLI API Grammar for ANTLR4 parser runtime.
//
// Author: Jordan Rieger
//         Software Development Manager - Webnames.ca Inc.
//         jordan@webnames.ca
//         www.webnames.ca
//
// Based on https://support.mailspec.com/en/guides/communigate-pro-manual/applications/data-formats/formal-syntax-rules as of 2024-10-24.
//
// TODO - The following elements are not yet implemented in this grammar as they are rarely used:
// - Single (//) and multi-line (/* ... */) comments. See https://support.mailspec.com/en/guides/communigate-pro-manual/textual-representation.
// - Embedded <xml/>. See https://support.mailspec.com/en/guides/communigate-pro-manual/xml-objects.
// - Other arbitrary objects. See https://support.mailspec.com/en/guides/communigate-pro-manual/atomic-objects#atomic-objects_other-objects.

grammar CGProCLI;

cliData: Whitespace* cliObject? ( Whitespace+ cliObject )* Whitespace* EOF; // Basic structure is a space-separated list of objects with optional surrounding whitespace.

cliObject: cliString | CLIInteger | CLINull | CLIIPAddress | cliDataBlock | cliArray | cliDictionary;

cliDataBlock: CLIDataBlock;

cliString: UnquotedString | QuotedString;

cliArray: 
	// Example: (Element1 , ("Sub Element1", SubElement2) , "Element 3")
	'(' Whitespace* cliObject? ( Whitespace* ',' Whitespace* cliObject )* Whitespace* ')';
	
cliDictionary: 
	// Example: {Key1=(Elem1,Elem2);  "$Key2" ={Sub1="XXX 1"; Sub2=X245 ;};}
	'{' ( Whitespace* cliString Whitespace* '=' Whitespace* cliObject Whitespace* ';' Whitespace* )* '}' ;

Whitespace: (' ' | '\t' | '\r' | '\n');

CLIDataBlock:
	// Base64-encoded binary blob, whitespace ignored.
	// Example: [DEAD BEEF +/1337/+ =]
	'[' (AlphaNumericChar | '+' | '/' | '=' | Whitespace )* ']';

UnquotedString:
	// Called an "Atom" in the CGPro docs.
	// Example: jordan@webnames.ca
	UnquotedStringSymbol+ |
	'login OK, proceed'; // The '200 login OK, proceed' response doesn't parse properly due to the comma, so we allow it explicitly here.
	
UnquotedStringSymbol: UnicodeSymbol | '.' | '-' | '@' | '_' | '<' | '>';

UnicodeSymbol: AlphaNumericChar | '\u{000080}'..'\u{FFFFFF}';

AlphaNumericChar: 'A'..'Z' | 'a'..'z' | DecimalDigit;

DecimalDigit: '0'..'9';

QuotedString:
	// Example: "foo\""
	'"' QuotedStringSymbol * '"';
	
QuotedStringSymbol:
	'\u{000000}'..'\u{000021}' | 
	'\u{000023}'..'\u{00005B}' |  
	'\u{00005D}'..'\u{FFFFFF}' | // All unicode characters except double-quote (") as those must surround the string, and backslash (\) for certain escape sequences.
	QuotedStringEscapeSequence;
	
QuotedStringEscapeSequence:
	'\\'
	(
		'"' |											// Escape sequence for double-quote is \"
		'\\' |											// Escape sequence for backslash is \\
		'r' |											// Escape sequence for carriage return is \r
		'n' |											// Escape sequence for newline is \n
		'e' |											// Escape sequence for end-of-line is \e
		't' |											// Escape sequence for tab is \t
		(DecimalDigit DecimalDigit DecimalDigit)	|	// Escape sequence for ASCII characters is \### where ### is any decimal number
		('u\'' HexDigit+ '\'')					 		// Escape sequence for Unicode characters is \u'HHHHHH' where HHHHHH is any hex number
	);

CLIInteger:
	// Examples: #-234657 (decimal) #0x17EF (hex) #-0b1000111000 (binary) #0o45374 (octal)
	'#' '-'? (DecimalDigit+ | '0x' HexDigit+ | '0o' OctalDigit+ | '0b' BinaryDigit+ );

CLITimeStamp:
	// Example: #T22-10-2009_15:24:45
	'#T'
	(
		'FUTURE' |
		'PAST' |
		DecimalDigit+ // Day
		'-'
		DecimalDigit+ // Month
		'-'
		DecimalDigit+ // Year
		(
			'_'
			DecimalDigit+ // Hour
			':'
			DecimalDigit+ // Minute
			':'
			DecimalDigit+ // Second
		)?
	);

CLINull:
	'#NULL#';

CLIIPAddress:
	// Examples: #I[10.0.44.55]:25 (IPv4), #I[2001:470:1f01:2565::a:80f]:2 (IPv6), both including an optional port suffix
	'#I'
	(
		( DecimalDigit+ '.' DecimalDigit+ '.' DecimalDigit+ '.' DecimalDigit+ ) |
		( HexDigit* ':' (HexDigit* ':' HexDigit+)+ )
	)
	( ':' DecimalDigit+ )?;

BinaryDigit: '0' | '1';

OctalDigit: '0'..'7';

HexDigit: DecimalDigit | 'A'..'F' | 'a'..'f';
