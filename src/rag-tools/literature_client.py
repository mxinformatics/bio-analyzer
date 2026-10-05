"""
Client for interacting with the BioAnalyzer ResearchApi LiteratureController.

This module provides a Python client for calling all endpoints exposed by the
LiteratureController in the BioAnalyzer.ResearchApi.
"""

from typing import List, Dict, Optional, Any
import requests
from dataclasses import dataclass


@dataclass
class EntrezSearchResult:
    """Model for Entrez search results."""
    count: str
    ret_max: str
    ret_start: str
    id_list: List[str]


@dataclass
class EntrezSummaryResult:
    """Model for Entrez summary results."""
    uid: str
    title: str
    pmc_id: str
    doi: str


@dataclass
class ArticleAbstract:
    """Model for article abstracts."""
    title: str
    description: str


@dataclass
class LiteratureDownloadLinkResult:
    """Model for literature download links."""
    pmc_id: str
    archive_link: str
    pdf_link: str


@dataclass
class LiteratureDownload:
    """Model for literature download information."""
    download_link: str
    file_name: str
    title: str
    pmc_id: str
    doi: str


@dataclass
class LiteratureDownloadList:
    """Model for list of literature downloads."""
    downloads: List[LiteratureDownload]


class LiteratureClient:
    """
    Client for interacting with the LiteratureController API.
    
    Provides methods to search literature, get summaries, abstracts,
    download links, and manage downloaded files.
    """

    def __init__(self, base_url: str, timeout: int = 30):
        """
        Initialize the Literature API client.
        
        Args:
            base_url: Base URL of the ResearchApi (e.g., "http://localhost:5000")
            timeout: Request timeout in seconds (default: 30)
        """
        self.base_url = base_url.rstrip('/')
        self.timeout = timeout
        self.session = requests.Session()

    def search_literature(
        self, 
        query: str, 
        start_index: int = 0
    ) -> EntrezSearchResult:
        """
        Search for literature using the provided query.
        
        Args:
            query: Search query string
            start_index: Starting index for pagination (default: 0)
            
        Returns:
            EntrezSearchResult containing search results
            
        Raises:
            ValueError: If query is empty or None
            requests.HTTPError: If the API request fails
        """
        if not query or not query.strip():
            raise ValueError("Query cannot be null or empty")
        
        url = f"{self.base_url}/Literature"
        params = {
            "query": query,
            "startIndex": start_index
        }
        
        response = self.session.get(url, params=params, timeout=self.timeout)
        response.raise_for_status()
        
        data = response.json()
        return EntrezSearchResult(
            count=data.get("count", ""),
            ret_max=data.get("retMax", ""),
            ret_start=data.get("retStart", ""),
            id_list=data.get("idList", [])
        )

    def get_summaries(self, ids: List[str]) -> List[EntrezSummaryResult]:
        """
        Get summaries for the provided literature IDs.
        
        Args:
            ids: List of literature IDs to retrieve summaries for
            
        Returns:
            List of EntrezSummaryResult objects
            
        Raises:
            requests.HTTPError: If the API request fails
        """
        if not ids:
            return []
        
        url = f"{self.base_url}/Literature/summary"
        params = {"ids": ids}
        
        response = self.session.get(url, params=params, timeout=self.timeout)
        response.raise_for_status()
        
        data = response.json()
        return [
            EntrezSummaryResult(
                uid=item.get("uid", ""),
                title=item.get("title", ""),
                pmc_id=item.get("pmcId", ""),
                doi=item.get("doi", "")
            )
            for item in data
        ]

    def get_abstract(self, pmc_id: str) -> ArticleAbstract:
        """
        Get the abstract for a specific literature article.
        
        Args:
            pmc_id: PubMed Central ID of the article
            
        Returns:
            ArticleAbstract object containing title and description
            
        Raises:
            requests.HTTPError: If the API request fails
        """
        url = f"{self.base_url}/Literature/abstract"
        params = {"pmcId": pmc_id}
        
        response = self.session.get(url, params=params, timeout=self.timeout)
        response.raise_for_status()
        
        data = response.json()
        return ArticleAbstract(
            title=data.get("title", ""),
            description=data.get("description", "")
        )

    def get_download_link(self, pmc_id: str) -> LiteratureDownloadLinkResult:
        """
        Get download links for a specific literature article.
        
        Args:
            pmc_id: PubMed Central ID of the article
            
        Returns:
            LiteratureDownloadLinkResult containing download links
            
        Raises:
            requests.HTTPError: If the API request fails
        """
        url = f"{self.base_url}/Literature/download"
        params = {"pmcId": pmc_id}
        
        response = self.session.get(url, params=params, timeout=self.timeout)
        response.raise_for_status()
        
        data = response.json()
        return LiteratureDownloadLinkResult(
            pmc_id=data.get("pmcId", ""),
            archive_link=data.get("archiveLink", ""),
            pdf_link=data.get("pdfLink", "")
        )

    def view_downloads(self) -> LiteratureDownloadList:
        """
        View the list of downloaded literature files.
        
        Returns:
            LiteratureDownloadList containing all downloaded files
            
        Raises:
            requests.HTTPError: If the API request fails
        """
        url = f"{self.base_url}/Literature/downloads/view"
        
        response = self.session.get(url, timeout=self.timeout)
        response.raise_for_status()
        
        data = response.json()
        downloads = [
            LiteratureDownload(
                download_link=item.get("downloadLink", ""),
                file_name=item.get("fileName", ""),
                title=item.get("title", ""),
                pmc_id=item.get("pmcId", ""),
                doi=item.get("doi", "")
            )
            for item in data.get("downloads", [])
        ]
        
        return LiteratureDownloadList(downloads=downloads)

    def download_file(self, file_name: str) -> bytes:
        """
        Download a specific literature file.
        
        Args:
            file_name: Name of the file to download
            
        Returns:
            File content as bytes (PDF format)
            
        Raises:
            requests.HTTPError: If the API request fails
        """
        url = f"{self.base_url}/Literature/downloads/{file_name}"
        
        response = self.session.get(url, timeout=self.timeout)
        response.raise_for_status()
        
        return response.content

    def save_file(self, file_name: str, output_path: str) -> None:
        """
        Download and save a literature file to disk.
        
        Args:
            file_name: Name of the file to download
            output_path: Path where the file should be saved
            
        Raises:
            requests.HTTPError: If the API request fails
            IOError: If file writing fails
        """
        content = self.download_file(file_name)
        with open(output_path, 'wb') as f:
            f.write(content)

    def close(self) -> None:
        """Close the underlying HTTP session."""
        self.session.close()

    def __enter__(self):
        """Context manager entry."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.close()
